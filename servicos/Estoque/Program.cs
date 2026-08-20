using Estoque;
using Estoque.Dados;
using Estoque.Modelos;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EstoqueDb>(opcoes =>
    opcoes.UseNpgsql(builder.Configuration.GetConnectionString("Padrao")));

// Única origem do projeto. Em produção viria do ambiente.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

// Um lugar só traduz exceção em resposta. Sem isso, cada endpoint acaba com
// o próprio try/catch e as mensagens divergem entre uma rota e outra.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Saldo insuficiente e código duplicado são respostas de negócio, não
    // falhas. Sem este filtro, cada uma vira "unhandled exception" com stack
    // trace no log — e a falha de verdade fica enterrada no meio delas.
    SuppressDiagnosticsCallback = contexto => contexto.Exception is
        ProdutoInvalidoException or ProdutoNaoEncontradoException or
        CodigoDuplicadoException or SaldoInsuficienteException or
        ConcorrenciaException or DbUpdateConcurrencyException,

    ExceptionHandler = async contexto =>
    {
        var erro = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;

        var (situacao, mensagem) = erro switch
        {
            ProdutoInvalidoException e => (StatusCodes.Status400BadRequest, e.Message),
            ProdutoNaoEncontradoException e => (StatusCodes.Status404NotFound, e.Message),
            CodigoDuplicadoException e => (StatusCodes.Status409Conflict, e.Message),
            SaldoInsuficienteException e => (StatusCodes.Status409Conflict, e.Message),
            ConcorrenciaException e => (StatusCodes.Status409Conflict, e.Message),
            // O token de concorrência protege todo UPDATE de produto, não só a baixa.
            // Sem esta linha, um PUT que perde a disputa vira 500 "erro inesperado".
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "Outra operação alterou este produto agora. Tente novamente."),
            _ => (StatusCodes.Status500InternalServerError, "Erro inesperado. Tente novamente.")
        };

        contexto.Response.StatusCode = situacao;
        await contexto.Response.WriteAsJsonAsync(new { erro = mensagem });
    }
});

using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<EstoqueDb>();
    await db.Database.MigrateAsync();
}

static void Validar(DadosProduto? dados)
{
    if (dados is null)
        throw new ProdutoInvalidoException("Corpo da requisição ausente ou inválido.");

    if (string.IsNullOrWhiteSpace(dados.Codigo))
        throw new ProdutoInvalidoException("O código do produto é obrigatório.");

    if (dados.Codigo.Trim().Length > 40)
        throw new ProdutoInvalidoException("O código do produto tem no máximo 40 caracteres.");

    if (string.IsNullOrWhiteSpace(dados.Descricao))
        throw new ProdutoInvalidoException("A descrição do produto é obrigatória.");

    if (dados.Descricao.Trim().Length > 200)
        throw new ProdutoInvalidoException("A descrição tem no máximo 200 caracteres.");

    if (dados.Saldo < 0)
        throw new ProdutoInvalidoException("O saldo não pode ser negativo.");
}

app.MapGet("/saude", () => Results.Ok(new { servico = "estoque", situacao = "no ar" }));

app.MapGet("/produtos", async (EstoqueDb db) =>
    await db.Produtos.OrderBy(p => p.Codigo).ToListAsync());

app.MapGet("/produtos/{id:int}", async (int id, EstoqueDb db) =>
    await db.Produtos.FindAsync(id) is Produto produto
        ? Results.Ok(produto)
        : throw new ProdutoNaoEncontradoException());

// A entrada é um DTO, não a entidade. Ligar o Produto direto deixaria o
// cliente escolher o Id e a Versao — o token de concorrência.
app.MapPost("/produtos", async (DadosProduto? dados, EstoqueDb db) =>
{
    Validar(dados);

    var produto = new Produto
    {
        Codigo = dados!.Codigo.Trim().ToUpperInvariant(),
        Descricao = dados.Descricao.Trim(),
        Saldo = dados.Saldo
    };

    if (await db.Produtos.AnyAsync(p => p.Codigo == produto.Codigo))
        throw new CodigoDuplicadoException(produto.Codigo);

    db.Produtos.Add(produto);
    await db.SaveChangesAsync();

    return Results.Created($"/produtos/{produto.Id}", produto);
});

app.MapPut("/produtos/{id:int}", async (int id, DadosProduto? dados, EstoqueDb db) =>
{
    var produto = await db.Produtos.FindAsync(id)
        ?? throw new ProdutoNaoEncontradoException();

    Validar(dados);

    var codigo = dados!.Codigo.Trim().ToUpperInvariant();

    if (codigo != produto.Codigo && await db.Produtos.AnyAsync(p => p.Codigo == codigo))
        throw new CodigoDuplicadoException(codigo);

    produto.Codigo = codigo;
    produto.Descricao = dados.Descricao.Trim();
    produto.Saldo = dados.Saldo;
    produto.Versao++;

    await db.SaveChangesAsync();

    return Results.Ok(produto);
});

app.MapDelete("/produtos/{id:int}", async (int id, EstoqueDb db) =>
{
    var produto = await db.Produtos.FindAsync(id)
        ?? throw new ProdutoNaoEncontradoException();

    db.Produtos.Remove(produto);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

// Baixa de saldo pedida pelo Faturamento na impressão da nota.
//
// Idempotente: o chamador manda uma chave que identifica a operação. Se a
// mesma chave voltar — porque o usuário clicou duas vezes, porque a rede caiu
// depois da baixa, ou porque houve retentativa — a resposta original é
// devolvida sem executar nada de novo.
app.MapPost("/saldos/baixa", async (
    BaixaRequisicao? pedido,
    EstoqueDb db,
    HttpContext contexto) =>
{
    var chave = contexto.Request.Headers["Idempotency-Key"].ToString();

    if (string.IsNullOrWhiteSpace(chave))
        throw new ProdutoInvalidoException("O cabeçalho Idempotency-Key é obrigatório.");

    if (chave.Length > 100)
        throw new ProdutoInvalidoException("A chave de idempotência tem no máximo 100 caracteres.");

    var jaFeita = await db.Idempotencias.FindAsync(chave);
    if (jaFeita is not null)
        return Results.Content(jaFeita.Resposta, "application/json");

    if (pedido is null || pedido.Itens is null || pedido.Itens.Count == 0)
        throw new ProdutoInvalidoException("Nenhum item informado para baixa.");

    await using var transacao = await db.Database.BeginTransactionAsync();

    var resultado = new List<BaixaItemResultado>();

    foreach (var item in pedido.Itens)
    {
        // Pode ter sido apagado depois que a nota foi criada. O Estoque não
        // tem como saber que existe nota apontando para ele — o cadastro de
        // notas é do outro serviço.
        var produto = await db.Produtos.FindAsync(item.ProdutoId)
            ?? throw new ProdutoNaoEncontradoException(
                $"O produto {item.ProdutoId} não existe mais no catálogo.");

        if (item.Quantidade <= 0)
            throw new ProdutoInvalidoException(
                $"A quantidade de {produto.Codigo} precisa ser maior que zero.");

        if (produto.Saldo < item.Quantidade)
            throw new SaldoInsuficienteException(
                $"Saldo insuficiente para {produto.Codigo}: disponível {produto.Saldo}, "
                + $"solicitado {item.Quantidade}.");

        produto.Saldo -= item.Quantidade;
        produto.Versao++;

        resultado.Add(new BaixaItemResultado(produto.Id, produto.Codigo, produto.Saldo));
    }

    // JsonSerializerDefaults.Web é o que o ASP.NET usa nas outras rotas. Sem
    // ele esta resposta sairia em PascalCase e seria a única do serviço assim.
    var resposta = System.Text.Json.JsonSerializer.Serialize(
        new { itens = resultado },
        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

    // O registro da chave entra na MESMA transação da baixa. Se a gravação
    // falhasse depois do commit do saldo, uma repetição baixaria de novo.
    db.Idempotencias.Add(new Idempotencia { Chave = chave, Resposta = resposta });

    try
    {
        await db.SaveChangesAsync();
        await transacao.CommitAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        // Alguém alterou o saldo entre a leitura e a gravação. A coluna Versao
        // é token de concorrência: o UPDATE só vale se a versão ainda for a
        // que foi lida, então quem chega em segundo não grava por cima.
        await transacao.RollbackAsync();
        throw new ConcorrenciaException();
    }
    catch (DbUpdateException e) when (ChaveDuplicada(e))
    {
        // Duas requisições com a mesma chave passaram juntas pela consulta lá
        // em cima e as duas chegaram até aqui. A chave é a chave primária, e
        // quem perde a disputa desfaz a própria baixa e devolve a resposta de
        // quem entrou — que é justamente o contrato da idempotência.
        await transacao.RollbackAsync();

        db.ChangeTracker.Clear();

        var vencedora = await db.Idempotencias.FindAsync(chave)
            ?? throw new ConcorrenciaException();

        return Results.Content(vencedora.Resposta, "application/json");
    }

    return Results.Content(resposta, "application/json");
});

app.Run();

// 23505 é a violação de unicidade do PostgreSQL.
static bool ChaveDuplicada(DbUpdateException e) =>
    e.InnerException is PostgresException { SqlState: "23505" };

public record DadosProduto(string Codigo, string Descricao, int Saldo);
public record BaixaItem(int ProdutoId, int Quantidade);
public record BaixaRequisicao(List<BaixaItem> Itens);
public record BaixaItemResultado(int ProdutoId, string Codigo, int SaldoAtual);
