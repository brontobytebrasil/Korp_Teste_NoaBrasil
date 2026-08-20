namespace Faturamento.Modelos;

public enum StatusNota
{
    Aberta = 0,
    Fechada = 1
}

public class Nota
{
    public int Id { get; set; }

    /// <summary>
    /// Numeração sequencial da nota, independente do id do banco.
    /// </summary>
    public int Numero { get; set; }

    public StatusNota Status { get; set; } = StatusNota.Aberta;

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;

    /// <summary>Quando a nota foi impressa. Nulo enquanto ela estiver aberta.</summary>
    public DateTime? ImpressaEm { get; set; }

    public List<NotaItem> Itens { get; set; } = [];
}

public class NotaItem
{
    public int Id { get; set; }

    public int NotaId { get; set; }

    /// <summary>
    /// Referência ao produto no serviço de Estoque. É só o identificador:
    /// o Faturamento não guarda cópia do cadastro nem do saldo, senão os
    /// dois serviços passariam a manter a mesma verdade em dois lugares.
    /// </summary>
    public int ProdutoId { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}
