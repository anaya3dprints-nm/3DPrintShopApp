using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDPrintStore.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteFileUploadColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CustomerEmail",
                table: "Orders",
                newName: "EmailAddress");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "QuoteRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasOwnModel",
                table: "QuoteRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "Orders",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentIntentId",
                table: "Orders",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "HasOwnModel",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StripePaymentIntentId",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "EmailAddress",
                table: "Orders",
                newName: "CustomerEmail");
        }
    }
}
