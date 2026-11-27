using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDPrintStore.Migrations
{
    /// <inheritdoc />
    public partial class AddOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubtotalCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ShippingCents = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    StripeSessionId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CheckoutToken = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StripePaymentIntentId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ShippingName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ShippingAddress = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PremiumCacheDrop = table.Column<bool>(type: "INTEGER", nullable: false),
                    SponsorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SocialHandle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SponsorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TrackingNumber = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    UnitPriceCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SendingUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderNotifications_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderNotifications_OrderId_Kind",
                table: "OrderNotifications",
                columns: new[] { "OrderId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StripeSessionId",
                table: "Orders",
                column: "StripeSessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "OrderNotifications");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
