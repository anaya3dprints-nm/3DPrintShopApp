using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDPrintStore.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceImagePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceImagePath",
                table: "PrintRequests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenceImagePath",
                table: "PrintRequests");
        }
    }
}
