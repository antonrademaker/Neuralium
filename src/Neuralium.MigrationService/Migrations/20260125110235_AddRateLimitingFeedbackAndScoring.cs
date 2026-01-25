using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields for arrays in migrations

namespace Neuralium.MigrationService.Migrations
{
    /// <inheritdoc />
    public partial class AddRateLimitingFeedbackAndScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumFetchIntervalMinutes",
                table: "feed_sources",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.CreateTable(
                name: "keyword_feedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TopicKeywordId = table.Column<int>(type: "integer", nullable: false),
                    FeedbackType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_keyword_feedbacks_topic_keywords_TopicKeywordId",
                        column: x => x.TopicKeywordId,
                        principalTable: "topic_keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "news_item_feedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NewsItemId = table.Column<int>(type: "integer", nullable: false),
                    FeedbackType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_item_feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_news_item_feedbacks_news_items_NewsItemId",
                        column: x => x.NewsItemId,
                        principalTable: "news_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "news_item_scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NewsItemId = table.Column<int>(type: "integer", nullable: false),
                    RecencyScore = table.Column<double>(type: "double precision", nullable: false),
                    TopicScore = table.Column<double>(type: "double precision", nullable: false),
                    UserFeedbackScore = table.Column<double>(type: "double precision", nullable: false),
                    ComputedTrendScore = table.Column<double>(type: "double precision", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_item_scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_news_item_scores_news_items_NewsItemId",
                        column: x => x.NewsItemId,
                        principalTable: "news_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_keyword_feedbacks_TopicKeywordId",
                table: "keyword_feedbacks",
                column: "TopicKeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_keyword_feedbacks_UserId",
                table: "keyword_feedbacks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_keyword_feedbacks_UserId_TopicKeywordId",
                table: "keyword_feedbacks",
                columns: new[] { "UserId", "TopicKeywordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_item_feedbacks_NewsItemId",
                table: "news_item_feedbacks",
                column: "NewsItemId");

            migrationBuilder.CreateIndex(
                name: "IX_news_item_feedbacks_UserId",
                table: "news_item_feedbacks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_news_item_feedbacks_UserId_NewsItemId",
                table: "news_item_feedbacks",
                columns: new[] { "UserId", "NewsItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_item_scores_CalculatedAtUtc",
                table: "news_item_scores",
                column: "CalculatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_news_item_scores_ComputedTrendScore",
                table: "news_item_scores",
                column: "ComputedTrendScore");

            migrationBuilder.CreateIndex(
                name: "IX_news_item_scores_NewsItemId",
                table: "news_item_scores",
                column: "NewsItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "keyword_feedbacks");

            migrationBuilder.DropTable(
                name: "news_item_feedbacks");

            migrationBuilder.DropTable(
                name: "news_item_scores");

            migrationBuilder.DropColumn(
                name: "MinimumFetchIntervalMinutes",
                table: "feed_sources");
        }
    }
}
