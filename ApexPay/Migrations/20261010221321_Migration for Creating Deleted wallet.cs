using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPay.Migrations
{
    /// <inheritdoc />
    public partial class MigrationforCreatingDeletedwallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_wallet_account_id_name",
                table: "wallets");

            migrationBuilder.CreateIndex(
                name: "uq_wallet_account_id_name",
                table: "wallets",
                columns: new[] { "account_id", "name" },
                unique: true,
                filter: "status <> 'CLOSED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_wallet_account_id_name",
                table: "wallets");

            migrationBuilder.CreateIndex(
                name: "uq_wallet_account_id_name",
                table: "wallets",
                columns: new[] { "account_id", "name" },
                unique: true);
        }
    }
}
