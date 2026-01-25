using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Neuralium.MigrationService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "news_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UrlHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IngestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RawContent = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TopicsJson = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EntitiesJson = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EmbeddingJson = table.Column<string>(type: "text", nullable: true),
                    TrendScore = table.Column<double>(type: "double precision", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_news_items_IsPublished",
                table: "news_items",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_news_items_PublishedAtUtc",
                table: "news_items",
                column: "PublishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_news_items_Source",
                table: "news_items",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_news_items_TrendScore",
                table: "news_items",
                column: "TrendScore");

            migrationBuilder.CreateIndex(
                name: "IX_news_items_UrlHash",
                table: "news_items",
                column: "UrlHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "news_items");
        }
    }
}
