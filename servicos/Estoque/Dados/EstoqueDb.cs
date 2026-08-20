using Estoque.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Dados;

public class EstoqueDb(DbContextOptions<EstoqueDb> opcoes) : DbContext(opcoes)
{
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Idempotencia> Idempotencias => Set<Idempotencia>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Produto>(produto =>
        {
            produto.HasKey(p => p.Id);
            produto.Property(p => p.Codigo).HasMaxLength(40).IsRequired();
            produto.Property(p => p.Descricao).HasMaxLength(200).IsRequired();
            produto.HasIndex(p => p.Codigo).IsUnique();

            produto.Property(p => p.Versao).IsConcurrencyToken();
        });

        modelo.Entity<Idempotencia>(registro =>
        {
            registro.HasKey(i => i.Chave);
            registro.Property(i => i.Chave).HasMaxLength(100);

            // A chave é a chave primária de propósito: duas requisições
            // simultâneas com a mesma chave disputam a inserção, e o banco
            // decide quem entra. A segunda recebe violação de unicidade em
            // vez de executar a baixa também.
            registro.HasIndex(i => i.CriadaEm);
        });
    }
}
