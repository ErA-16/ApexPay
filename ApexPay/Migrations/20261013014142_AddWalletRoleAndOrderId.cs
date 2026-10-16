using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPay.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletRoleAndOrderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "wallets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "USER");

            migrationBuilder.AddColumn<string>(
                name: "order_id",
                table: "transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role",
                table: "wallets");

            migrationBuilder.DropColumn(
                name: "order_id",
                table: "transactions");
        }
    }
}
