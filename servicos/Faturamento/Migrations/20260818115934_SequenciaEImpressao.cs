using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faturamento.Migrations
{
    /// <inheritdoc />
    public partial class SequenciaEImpressao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "numero_nota");

            migrationBuilder.AddColumn<DateTime>(
                name: "ImpressaEm",
                table: "Notas",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImpressaEm",
                table: "Notas");

            migrationBuilder.DropSequence(
                name: "numero_nota");
        }
    }
}
