using System.Net;

namespace Faturamento.Servicos;

public record ProdutoResumo(int Id, string Codigo, string Descricao, int Saldo);

public record BaixaItem(int ProdutoId, int Quantidade);
public record BaixaRequisicao(List<BaixaItem> Itens);

public class EstoqueClient(HttpClient http, ILogger<EstoqueClient> log)
{
    private record RespostaErro(string Erro);

    public async Task<IReadOnlyList<ProdutoResumo>> ListarProdutosAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsync<List<ProdutoResumo>>("/produtos", ct) ?? [];
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(erro, "Estoque não respondeu ao listar produtos");
            throw new EstoqueIndisponivelException(
                "O serviço de estoque não está respondendo. Tente novamente em instantes.");
        }
    }

    /// <param name="chave">
    /// Identifica a operação de forma estável. Usamos o id da nota: a mesma
    /// nota impressa duas vezes manda a mesma chave, e o Estoque reconhece
    /// que já baixou aquele saldo em vez de baixar de novo.
    /// </param>
    public async Task BaixarSaldoAsync(
        BaixaRequisicao pedido,
        string chave,
        CancellationToken ct = default)
    {
        HttpResponseMessage resposta;

        try
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/saldos/baixa")
            {
                Content = JsonContent.Create(pedido)
            };
            requisicao.Headers.Add("Idempotency-Key", chave);

            resposta = await http.SendAsync(requisicao, ct);
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(erro, "Estoque não respondeu à baixa de saldo");
            throw new EstoqueIndisponivelException(
                "O serviço de estoque não está respondendo. A nota continua aberta.");
        }

        if (resposta.IsSuccessStatusCode) return;

        // Erro do servidor é indisponibilidade; recusa de negócio é outra coisa.
        if (resposta.StatusCode >= HttpStatusCode.InternalServerError)
            throw new EstoqueIndisponivelException(
                "O serviço de estoque falhou ao baixar o saldo. A nota continua aberta.");

        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaErro>(ct);
        throw new EstoqueRecusouException(corpo?.Erro ?? "O estoque recusou a baixa.");
    }
}
