using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Neuralium.MigrationService.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SummaryVersion",
                table: "news_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SummaryVersion",
                table: "news_items");
        }
    }
}
