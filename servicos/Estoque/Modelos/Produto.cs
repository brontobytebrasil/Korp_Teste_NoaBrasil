namespace Estoque.Modelos;

public class Produto
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public int Saldo { get; set; }

    /// <summary>
    /// Muda a cada gravação. O banco recusa a escrita se a versão lida
    /// não for mais a atual, o que impede duas notas de baixarem o mesmo saldo.
    /// </summary>
    public int Versao { get; set; }
}
