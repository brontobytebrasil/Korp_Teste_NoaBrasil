namespace Estoque.Modelos;

/// <summary>
/// Registro de uma operação já executada, identificada pela chave que o
/// chamador enviou. Se a mesma chave voltar, a resposta guardada é devolvida
/// em vez de a operação rodar de novo.
/// </summary>
public class Idempotencia
{
    public string Chave { get; set; } = string.Empty;

    /// <summary>Corpo da resposta original, em JSON.</summary>
    public string Resposta { get; set; } = string.Empty;

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}
