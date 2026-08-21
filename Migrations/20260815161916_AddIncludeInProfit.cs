using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldShop.Migrations
{
    /// <inheritdoc />
    public partial class AddIncludeInProfit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeInProfit",
                table: "ClientTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeInProfit",
                table: "ClientTransactions");
        }
    }
}
