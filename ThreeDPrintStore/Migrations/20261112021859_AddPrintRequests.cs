using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDPrintStore.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrintRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ProjectDescription = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    MaterialType = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedColorsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrintQuotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrintRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilamentGrams = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedPrintHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ColorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialCostPerGram = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaterialSubtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LaborMachineSubtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MultiColorSurcharge = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseHandlingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalTotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StripePaymentIntentId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintQuotes_PrintRequests_PrintRequestId",
                        column: x => x.PrintRequestId,
                        principalTable: "PrintRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintQuotes_PrintRequestId",
                table: "PrintQuotes",
                column: "PrintRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintQuotes");

            migrationBuilder.DropTable(
                name: "PrintRequests");
        }
    }
}
