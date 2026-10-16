using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPay.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletAccountIdCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_wallet_account_id_created_at",
                table: "wallets",
                columns: new[] { "account_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wallet_account_id_created_at",
                table: "wallets");
        }
    }
}
