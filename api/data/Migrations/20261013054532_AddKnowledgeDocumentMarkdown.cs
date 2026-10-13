using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentMarkdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarkdownText",
                table: "KnowledgeDocuments",
                type: "TEXT",
                maxLength: 100000000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkdownText",
                table: "KnowledgeDocuments");
        }
    }
}
