using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentMarkdownSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarkdownSource",
                table: "KnowledgeDocuments",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkdownSource",
                table: "KnowledgeDocuments");
        }
    }
}
