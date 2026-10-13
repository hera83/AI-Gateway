using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentMarkdownVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarkdownLlmDraft",
                table: "KnowledgeDocuments",
                type: "TEXT",
                maxLength: 100000000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarkdownLlmPartsDone",
                table: "KnowledgeDocuments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarkdownLlmPartsTotal",
                table: "KnowledgeDocuments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarkdownVersion",
                table: "KnowledgeDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkdownLlmDraft",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "MarkdownLlmPartsDone",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "MarkdownLlmPartsTotal",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "MarkdownVersion",
                table: "KnowledgeDocuments");
        }
    }
}
