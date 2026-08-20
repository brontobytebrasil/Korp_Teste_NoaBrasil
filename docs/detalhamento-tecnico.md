# Detalhamento técnico

Respostas aos itens pedidos na especificação, na mesma ordem, com o caminho do
arquivo de cada uma.

## Como o sistema está dividido

São dois microsserviços e uma aplicação web.

O **Estoque** cuida de produto e saldo. O **Faturamento** cuida da nota fiscal.
Cada um tem o seu próprio banco. Poderia ter deixado os dois na mesma base e
seria mais simples, mas aí a separação seria só de fachada: dois processos
lendo a mesma tabela é um monólito distribuído, com a desvantagem da rede e
nenhuma das vantagens do isolamento.

O Faturamento nunca altera saldo direto. Na impressão da nota ele pede a baixa
ao Estoque por HTTP. Isso concentra a escrita do saldo num serviço só, que é o
que permite tratar concorrência em um lugar em vez de espalhar trava pelo
sistema inteiro.

Uma consequência disso que vale explicar: ao criar a nota, o navegador manda o
código do produto e o Faturamento resolve o id contra o catálogo do Estoque. Ele
poderia aceitar o id que veio da tela e não dependeria do Estoque nesse momento
— mas aí o cliente escolheria para qual produto a nota aponta, inclusive um que
ele nunca viu na lista. Preferi a dependência: com o Estoque fora, a criação
responde 503 com mensagem clara, e na prática a tela já estava inutilizável
porque o catálogo também vem de lá. Listar notas continua funcionando.

---

## Ciclos de vida do Angular utilizados

`web/src/app/produtos/produtos.ts` · `web/src/app/notas/notas.ts`

Uso `ngOnInit` para buscar dado quando o componente entra em cena, e
`ngOnDestroy` para encerrar as inscrições quando ele sai. A terceira tela,
`diagnostico.ts`, não implementa nenhum dos dois de propósito: ela não busca
nada ao entrar, só age no clique.

Não faço a busca no construtor. O construtor serve para receber dependência; se
eu disparar requisição ali, o componente começa a trabalhar antes de estar
pronto e o teste fica mais difícil de escrever.

O `ngOnDestroy` é o que evita o problema mais comum de Angular: a resposta de
uma requisição chegando para um componente que já morreu. Uso um `Subject`
chamado `encerrar$` e o operador `takeUntil` em toda inscrição — no destroy eu
emito e completo esse Subject, e todas terminam junto.

```ts
private readonly encerrar$ = new Subject<void>();

ngOnInit(): void {
  this.carregar();
}

ngOnDestroy(): void {
  this.encerrar$.next();
  this.encerrar$.complete();
}

private carregar(): void {
  this.carregando = true;
  this.produtoService
    .listar()
    .pipe(takeUntil(this.encerrar$))
    .subscribe({
      next: (lista) => {
        this.produtos = lista;
        this.carregando = false;
      },
      error: (falha) => {
        this.mostrarErro(falha, 'Não foi possível carregar os produtos.');
        this.carregando = false;
      },
    });
}
```

---

## Uso de RxJS

`web/src/app/produto.service.ts` · `web/src/app/nota.service.ts`

Sim. Todo acesso ao servidor devolve `Observable`, e eu componho com `pipe` em
vez de encadear `subscribe` dentro de `subscribe`.

O serviço devolve o Observable puro e não se inscreve. Quem decide quando
executar é o componente, porque é ele quem sabe quando a tela morre. Serviço que
se inscreve sozinho tira essa decisão de quem tem a informação.

O uso que mais importa aqui é o `takeUntil` ligado ao ciclo de vida, descrito
acima. É o que transforma o cancelamento em algo automático em vez de depender
de alguém lembrar de chamar `unsubscribe`.

A exceção é a tela de Demonstração (`web/src/app/diagnostico/diagnostico.ts`),
que usa `fetch` direto. Ali o código HTTP da resposta **é** o resultado que se
quer mostrar, inclusive quando é 400 ou 409 — e o `HttpClient` trata esses
códigos como erro e desvia do fluxo. Usar a ferramenta errada ali daria mais
código para conseguir menos.

---

## Outras bibliotecas utilizadas

**Backend** — `servicos/*/*.csproj`

- **Entity Framework Core** como ORM, com migrações versionadas no
  repositório e aplicadas na subida de cada serviço. Assim o avaliador não
  precisa rodar nada de banco na mão.
- **Npgsql** como provedor do PostgreSQL.

Removi os dois pacotes de OpenAPI que vieram no template: não há `AddOpenApi`
nem `MapOpenApi` em lugar nenhum, então eram peso morto — e um deles tinha
vulnerabilidade de severidade alta (GHSA-v5pm-xwqc-g5wc). Hoje
`dotnet list package --vulnerable` volta limpo nos dois serviços e
`npm audit` volta limpo no front.

**Front** — `web/package.json`

- **Angular Material** para os componentes.
- **RxJS**, que já acompanha o Angular.

Não trouxe nada além disso. Cada dependência é código de terceiro que passa a
ser meu problema quando quebra, então só entra o que resolve algo que eu não
resolveria melhor sozinho.

---

## Bibliotecas de componentes visuais

`web/src/app/produtos/produtos.ts` · `web/src/app/notas/notas.ts`

Angular Material. O que uso e por quê:

| Componente | Onde |
| --- | --- |
| `mat-table` | listagem de produtos e de notas |
| `mat-form-field` com formulário reativo | cadastro de produto (`produtos.ts`), com validação por campo |
| `mat-form-field` com `ngModel` | montagem da nota (`notas.ts`), onde não há formulário a validar |
| `mat-select` | escolha do produto ao montar a nota |
| `mat-chip` | itens da nota antes de salvar, com remoção individual |
| `mat-progress-spinner` | indicador de processamento na impressão — é exigência da especificação |
| `mat-snack-bar` | retorno do servidor ao usuário |

O spinner da impressão fica **na linha da nota**, não na tabela inteira. Guardo
o id da nota em impressão justamente para isso: travar a tela toda enquanto uma
linha processa passa a impressão errada de que o sistema parou.

---

## Gerenciamento de dependências no Golang

Não se aplica: o backend foi feito em C#.

Escolhi C# porque tenho trabalho real com .NET e a especificação pergunta sobre
LINQ, o que indica que quem avalia conhece bem esse lado. Preferi entregar no
terreno onde a qualidade é reconhecível a arriscar Go, que eu não uso.

As dependências são declaradas como `PackageReference` no arquivo de projeto e
restauradas com `dotnet restore`. A versão do SDK está fixada em `global.json`,
para o projeto compilar igual em qualquer máquina — esta aqui tem dois SDKs
instalados e sem o arquivo o `dotnet` escolheria o errado.

---

## Frameworks utilizados no backend

`servicos/Estoque/Program.cs` · `servicos/Faturamento/Program.cs`

ASP.NET Core sobre .NET 10, usando **Minimal API**.

Escolhi Minimal API porque em projeto deste tamanho o fluxo de cada endpoint
fica visível num arquivo só. Controller com camadas faria sentido se houvesse
muito mais rota ou mais gente mexendo; aqui adicionaria arquivo sem adicionar
clareza.

Entity Framework Core para acesso a dados.

---

## Tratamento de erros e exceções no backend

`servicos/Estoque/Erros.cs` · `servicos/Faturamento/Erros.cs` ·
`UseExceptionHandler` no `Program.cs` de cada serviço

As exceções do projeto dizem o que aconteceu, não onde quebrou:
`SaldoInsuficienteException`, `CodigoDuplicadoException`, `NotaJaFechadaException`.
Um único handler traduz cada uma no código HTTP correspondente e devolve sempre
o mesmo formato.

```csharp
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
```

Nenhum endpoint tem `try/catch` próprio. Quando cada rota trata o seu erro, as
mensagens divergem com o tempo e o usuário recebe respostas diferentes para o
mesmo problema.

Duas regras que sigo: **nunca engolir erro** em catch vazio, porque erro
silencioso é pior que erro estourado — ele aparece semanas depois como dado
errado; e **nunca devolver detalhe interno**, então o inesperado vira mensagem
genérica para o usuário e detalhe no log do servidor.

Há uma distinção que considero a parte mais importante desta seção. O
Faturamento separa duas coisas que parecem uma só:

- **O Estoque não respondeu** → `503`. É infraestrutura. A ação do usuário é
  tentar de novo daqui a pouco.
- **O Estoque respondeu recusando**, por saldo insuficiente → `409`. É negócio.
  Tentar de novo não adianta; é preciso resolver o saldo.

Tratar as duas como "deu erro" jogaria o usuário contra uma parede sem saber
para que lado andar.

O saldo é verificado em dois momentos, e isso não é redundância.

Na **criação da nota**, o Faturamento compara o total pedido com o saldo do
catálogo e recusa com `400` se não couber. Nota que já nasce impossível de
imprimir só encheria a lista de documento morto.

Na **impressão**, o Estoque verifica de novo, dentro da transação da baixa. Essa
é a que vale: entre criar a nota e imprimir, outra operação pode levar o saldo, e
nenhuma checagem feita antes consegue prometer nada sobre esse intervalo.

Dá para ver as duas trabalhando: crio uma nota de 1 unidade de um produto com
saldo 1 — passa. Antes de imprimir, outra baixa leva a última unidade. A
impressão então recusa com `409`, a nota continua aberta e o saldo não vai a
negativo. A primeira checagem é conveniência; a segunda é a garantia.

Verifiquei também o caso de nota com dois itens onde só o segundo falta saldo: a
baixa roda dentro de uma transação, então o primeiro item **não** é descontado.
Nada fica pela metade.

---

## Uso de LINQ

`servicos/Faturamento/Program.cs` · `servicos/Estoque/Program.cs`

Sim, com Entity Framework.

O ponto que importa é **onde a consulta roda**. Quando escrevo `Where`,
`Include` e `OrderBy` sobre o `DbSet`, isso é traduzido em SQL e executado no
banco. Só materializo no fim, com `ToListAsync`.

```csharp
await db.Notas
    .Include(n => n.Itens)
    .OrderByDescending(n => n.Numero)
    .ToListAsync();
```

Se eu chamasse `ToList()` antes do `Where`, traria a tabela inteira para a
memória e filtraria em C#. Funcionaria com dez registros e derrubaria a
aplicação com um milhão — e o pior é que o código pareceria idêntico.

Uso LINQ em memória também, mas só sobre coleção que já está carregada, como ao
transformar os itens da nota no formato do pedido de baixa. Aí não há banco
envolvido e a diferença não existe.

---

## Requisitos obrigatórios

### Arquitetura de microsserviços

Estoque e Faturamento, cada um com banco próprio, comunicando por HTTP com
cliente tipado e tempo limite curto.

### Tratamento de falhas

`servicos/Faturamento/Servicos/EstoqueClient.cs`

Com o Estoque fora do ar, o Faturamento continua respondendo. Ao tentar
imprimir, a nota **permanece aberta**, o saldo **não é tocado** e o usuário
recebe uma mensagem dizendo o que houve. Subindo o serviço, a mesma impressão
conclui.

Para reproduzir:

```sh
docker compose stop estoque     # tente imprimir uma nota aberta
docker compose start estoque    # imprima de novo, agora conclui
```

A ordem no código é intencional: a nota só é fechada **depois** que a baixa foi
aceita. Fechar antes deixaria nota fechada com saldo intacto — e ninguém
descobriria até o estoque não bater.

### Conexão real com banco

PostgreSQL 16 em container, um banco por serviço, criados na subida. Migrações
versionadas no repositório e aplicadas automaticamente.

O banco é publicado em **5433** e não na 5432 porque a porta padrão costuma já
estar ocupada por uma instalação local do PostgreSQL. Dentro da rede do Compose
os serviços continuam usando 5432.

---

## Requisitos opcionais

Dois dos três: idempotência e concorrência. A tela **Demonstração**, em
`/diagnostico`, executa os dois cenários e mostra o resultado com os números
reais. Ela é separada do fluxo de uso e existe para a avaliação — está
identificada como tal na própria tela.

O terceiro, **inteligência artificial, eu decidi não implementar** — e a
decisão é parte da resposta.

O único uso que caberia nesta tela seria interpretar um pedido escrito em
texto livre e transformá-lo nos itens da nota. Só que a montagem da nota já
tem o catálogo num campo de seleção, com código, descrição e saldo à vista.
Trocar dois cliques por uma frase digitada não economiza tempo de ninguém, e
em troca acrescenta uma dependência externa, uma chave para gerenciar, uma
chamada de rede que pode falhar e uma resposta que ainda precisa ser
conferida antes de virar nota.

IA se paga onde a entrada é ambígua ou o canal não comporta formulário: um
assistente por voz, uma conversa por mensagem, um documento sem estrutura.
Aqui existe formulário, e ele resolve melhor. Preferi entregar dois opcionais
sólidos e o critério do terceiro.

### Idempotência

`servicos/Estoque/Program.cs`, endpoint `/saldos/baixa`

A baixa exige o cabeçalho `Idempotency-Key`. Se a mesma chave voltar, a resposta
original é devolvida sem executar nada de novo. Isso protege contra clique
duplo, retentativa e queda de rede depois de a baixa já ter acontecido.

A chave é a chave primária da tabela, não um campo com índice único. Duas
requisições simultâneas com a mesma chave passam juntas pela consulta inicial e
disputam a inserção; quem perde recebe a violação de unicidade do Postgres,
desfaz a própria baixa e devolve a resposta de quem entrou. A garantia é do
banco, não de um `if` no meu código.

O registro da chave entra na mesma transação da baixa. Se eu gravasse depois do
commit do saldo, existiria uma janela — saldo baixado, chave não registrada — e
uma repetição baixaria de novo.

E a chave é o id da nota, não um valor aleatório: `nota-3` é sempre `nota-3`.
Chave gerada a cada requisição não protegeria nada, porque cada tentativa teria
uma chave nova.

Essa chave estável dá um efeito colateral bom, que é o mais forte da entrega: o
sistema se recupera sozinho. Se o Faturamento cair **depois** da baixa e antes
de fechar a nota, a reimpressão manda a mesma chave, o Estoque devolve a
resposta guardada sem baixar de novo, e a nota fecha. Nenhum passo manual, nem
saldo baixado duas vezes.

A tabela de chaves cresce para sempre, e eu não escrevi rotina de limpeza.
Cheguei a escrever: um serviço em segundo plano que apagava chaves com mais de
sete dias, a cada seis horas. Tirei, porque num projeto de dias esse código
nunca chega a executar o caminho que importa — é rotina que nem eu vi rodar,
com dois números escolhidos sem critério nenhum. Em produção isso seria um job
de retenção ou um TTL na tabela, calibrado pela janela real de retentativa do
sistema. Preferi dizer isso a entregar código que ninguém exercitou.

### Concorrência

O cenário que a especificação cita: produto com saldo 1 disputado por duas notas.

São duas travas. A verificação de saldo acontece **dentro da transação**, e a
coluna `Versao` do produto é **token de concorrência** — o UPDATE só é aceito se
a versão ainda for a que foi lida. Quem chega em segundo não grava por cima e
recebe aviso para tentar de novo.

Resultado: uma baixa conclui, a outra é recusada com mensagem clara, e o saldo
termina em zero. Nunca negativo.

---

## O que eu faria em seguida

Não há testes automatizados neste projeto — e não deixei o esqueleto: tirei o
karma, o jasmine e o alvo `test` que vieram no template, porque arnês de teste
sem um único teste dentro sugere que ficaram pela metade. No prazo do desafio,
com escopo cheio, priorizei entregar o comportamento funcionando e verificável. Se este código
fosse continuar com mais gente mexendo, os dois primeiros testes que eu
escreveria seriam exatamente os dois comportamentos mais fáceis de quebrar sem
perceber: chamar a baixa duas vezes com a mesma chave e afirmar que o saldo caiu
uma vez só; e disparar duas baixas concorrentes sobre saldo 1 e afirmar que o
saldo final é zero.

Além disso: paginação nas listagens, que hoje trazem tudo; e um identificador de
correlação atravessando os dois serviços, para conseguir seguir uma impressão
nos dois logs quando algo der errado em produção.
