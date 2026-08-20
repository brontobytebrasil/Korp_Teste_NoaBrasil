using Faturamento;
using Faturamento.Dados;
using Faturamento.Modelos;
using Faturamento.Servicos;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FaturamentoDb>(opcoes =>
    opcoes.UseNpgsql(builder.Configuration.GetConnectionString("Padrao")));

builder.Services.AddHttpClient<EstoqueClient>(cliente =>
{
    cliente.BaseAddress = new Uri(builder.Configuration["Estoque:Url"]!);
    // Tempo curto de propósito: se o Estoque está fora, é melhor falhar rápido
    // com mensagem clara do que deixar o usuário esperando sem saber.
    cliente.Timeout = TimeSpan.FromSeconds(5);
});

// Única origem do projeto. Em produção viria do ambiente.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Nota já fechada e recusa do Estoque são respostas de negócio, não
    // falhas. EstoqueIndisponivelException fica FORA da lista de propósito:
    // serviço no chão é problema de infraestrutura e tem que aparecer no log.
    SuppressDiagnosticsCallback = contexto => contexto.Exception is
        NotaInvalidaException or NotaNaoEncontradaException or
        NotaJaFechadaException or EstoqueRecusouException,

    ExceptionHandler = async contexto =>
    {
        var erro = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;

        var (situacao, mensagem) = erro switch
        {
            NotaInvalidaException e => (StatusCodes.Status400BadRequest, e.Message),
            NotaNaoEncontradaException e => (StatusCodes.Status404NotFound, e.Message),
            NotaJaFechadaException e => (StatusCodes.Status409Conflict, e.Message),
            EstoqueRecusouException e => (StatusCodes.Status409Conflict, e.Message),
            EstoqueIndisponivelException e => (StatusCodes.Status503ServiceUnavailable, e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro inesperado. Tente novamente.")
        };

        contexto.Response.StatusCode = situacao;
        await contexto.Response.WriteAsJsonAsync(new { erro = mensagem });
    }
});

using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<FaturamentoDb>();
    await db.Database.MigrateAsync();
}

app.MapGet("/saude", () => Results.Ok(new { servico = "faturamento", situacao = "no ar" }));

app.MapGet("/notas", async (FaturamentoDb db) =>
    await db.Notas.Include(n => n.Itens).OrderByDescending(n => n.Numero).ToListAsync());

app.MapGet("/notas/{id:int}", async (int id, FaturamentoDb db) =>
    await db.Notas.Include(n => n.Itens).FirstOrDefaultAsync(n => n.Id == id) is Nota nota
        ? Results.Ok(nota)
        : throw new NotaNaoEncontradaException());

app.MapPost("/notas", async (
    NovaNota? pedido,
    FaturamentoDb db,
    EstoqueClient estoque,
    CancellationToken ct) =>
{
    if (pedido?.Itens is null || pedido.Itens.Count == 0)
        throw new NotaInvalidaException("A nota precisa de pelo menos um item.");

    if (pedido.Itens.Any(i => i.Quantidade <= 0))
        throw new NotaInvalidaException("A quantidade de cada item precisa ser maior que zero.");

    // O navegador manda o código; o id e a descrição vêm do catálogo do Estoque.
    // Aceitar o id que veio da tela seria deixar o cliente apontar para
    // qualquer produto — inclusive um que ele não viu na lista.
    var catalogo = await estoque.ListarProdutosAsync(ct);

    // Agrupa por código: o mesmo produto pode vir em duas linhas, e o que
    // importa para o saldo é o total pedido.
    var itens = pedido.Itens
        .GroupBy(i => (i.Codigo ?? string.Empty).Trim().ToUpperInvariant())
        .Select(grupo =>
        {
            var produto = catalogo.FirstOrDefault(p => p.Codigo == grupo.Key)
                ?? throw new NotaInvalidaException($"Produto {grupo.Key} não existe no catálogo.");

            var quantidade = grupo.Sum(i => i.Quantidade);

            // Nota que já nasce sem saldo nunca vai imprimir. Barro aqui para
            // não deixar documento morto na lista.
            //
            // Isto NÃO substitui a verificação da impressão: entre criar e
            // imprimir, outra nota pode levar o saldo. Aqui é conveniência;
            // a garantia é a do Estoque, dentro da transação da baixa.
            if (quantidade > produto.Saldo)
                throw new NotaInvalidaException(
                    $"Saldo insuficiente para {produto.Codigo}: disponível {produto.Saldo}, "
                    + $"solicitado {quantidade}.");

            return new NotaItem
            {
                ProdutoId = produto.Id,
                Codigo = produto.Codigo,
                Quantidade = quantidade
            };
        })
        .ToList();

    var nota = new Nota
    {
        Numero = await db.ProximoNumeroAsync(),
        Status = StatusNota.Aberta,
        Itens = itens
    };

    db.Notas.Add(nota);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/notas/{nota.Id}", nota);
});

// Impressão: baixa o saldo no Estoque e fecha a nota. Se a baixa falhar,
// a nota permanece aberta — nada é gravado pela metade.
app.MapPost("/notas/{id:int}/imprimir", async (
    int id,
    FaturamentoDb db,
    EstoqueClient estoque,
    CancellationToken ct) =>
{
    var nota = await db.Notas.Include(n => n.Itens).FirstOrDefaultAsync(n => n.Id == id, ct)
        ?? throw new NotaNaoEncontradaException();

    if (nota.Status != StatusNota.Aberta)
        throw new NotaJaFechadaException();

    var baixa = new BaixaRequisicao(
        nota.Itens.Select(i => new BaixaItem(i.ProdutoId, i.Quantidade)).ToList());

    // Chave estável: a mesma nota sempre produz a mesma chave, então
    // repetir a impressão não baixa o saldo duas vezes.
    await estoque.BaixarSaldoAsync(baixa, $"nota-{nota.Id}", ct);

    nota.Status = StatusNota.Fechada;
    nota.ImpressaEm = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);

    return Results.Ok(nota);
});

app.Run();

public record NovoItem(string Codigo, int Quantidade);
public record NovaNota(List<NovoItem> Itens);
