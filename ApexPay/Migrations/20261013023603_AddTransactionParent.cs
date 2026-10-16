using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPay.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_transaction_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_transaction_parent",
                table: "transactions",
                column: "parent_transaction_id");

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_transactions_parent_transaction_id",
                table: "transactions",
                column: "parent_transaction_id",
                principalTable: "transactions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_transactions_parent_transaction_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transaction_parent",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "parent_transaction_id",
                table: "transactions");
        }
    }
}
