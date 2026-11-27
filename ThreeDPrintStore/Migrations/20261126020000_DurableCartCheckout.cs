using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDPrintStore.Migrations
{
    public partial class DurableCartCheckout : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveCartKey", table: "Orders", type: "TEXT", maxLength: 64, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "CartKey", table: "Orders", type: "TEXT", maxLength: 64, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "CheckoutOrigin", table: "Orders", type: "TEXT", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "NextCartIdentity", table: "Orders", type: "TEXT", maxLength: 500, nullable: true);

            migrationBuilder.CreateTable(
                name: "CheckoutSubmissions",
                columns: table => new
                {
                    Token = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutSubmissions", x => x.Token);
                    table.ForeignKey(
                        name: "FK_CheckoutSubmissions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ActiveCartKey", table: "Orders", column: "ActiveCartKey", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSubmissions_OrderId", table: "CheckoutSubmissions", column: "OrderId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CheckoutSubmissions");
            migrationBuilder.DropIndex(name: "IX_Orders_ActiveCartKey", table: "Orders");
            migrationBuilder.DropColumn(name: "ActiveCartKey", table: "Orders");
            migrationBuilder.DropColumn(name: "CartKey", table: "Orders");
            migrationBuilder.DropColumn(name: "CheckoutOrigin", table: "Orders");
            migrationBuilder.DropColumn(name: "NextCartIdentity", table: "Orders");
        }
    }
}
