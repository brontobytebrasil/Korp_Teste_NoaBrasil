using Faturamento.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Dados;

public class FaturamentoDb(DbContextOptions<FaturamentoDb> opcoes) : DbContext(opcoes)
{
    public DbSet<Nota> Notas => Set<Nota>();
    public DbSet<NotaItem> NotaItens => Set<NotaItem>();

    /// <summary>
    /// Próximo número da nota, vindo de uma sequência do banco.
    /// Fazer MAX(numero) + 1 daria número repetido se duas notas fossem
    /// criadas ao mesmo tempo: as duas leriam o mesmo máximo.
    /// </summary>
    public async Task<int> ProximoNumeroAsync()
    {
        var conexao = Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
            await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT nextval('numero_nota')";
        var valor = await comando.ExecuteScalarAsync();

        return Convert.ToInt32(valor);
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasSequence<int>("numero_nota").StartsAt(1).IncrementsBy(1);

        modelo.Entity<Nota>(nota =>
        {
            nota.HasKey(n => n.Id);
            nota.HasIndex(n => n.Numero).IsUnique();
            nota.Property(n => n.Status).HasConversion<int>();

            nota.HasMany(n => n.Itens)
                .WithOne()
                .HasForeignKey(i => i.NotaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelo.Entity<NotaItem>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Codigo).HasMaxLength(40).IsRequired();
        });
    }
}
