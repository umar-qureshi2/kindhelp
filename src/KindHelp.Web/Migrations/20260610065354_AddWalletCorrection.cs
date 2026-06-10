using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KindHelp.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Method",
                table: "Contributions");

            migrationBuilder.AddColumn<int>(
                name: "CorrectsWalletTransactionId",
                table: "WalletTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Donors",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_CorrectsWalletTransactionId",
                table: "WalletTransactions",
                column: "CorrectsWalletTransactionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Donors_PhoneNormalized",
                table: "Donors",
                sql: "\"PhoneNumber\" IS NULL OR \"PhoneNumber\" ~ '^\\+?[0-9]+$'");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactions_WalletTransactions_CorrectsWalletTransac~",
                table: "WalletTransactions",
                column: "CorrectsWalletTransactionId",
                principalTable: "WalletTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactions_WalletTransactions_CorrectsWalletTransac~",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_CorrectsWalletTransactionId",
                table: "WalletTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Donors_PhoneNormalized",
                table: "Donors");

            migrationBuilder.DropColumn(
                name: "CorrectsWalletTransactionId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Donors");

            migrationBuilder.AddColumn<int>(
                name: "Method",
                table: "Contributions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
