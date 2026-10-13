using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentIndexVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IndexVersion",
                table: "KnowledgeDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndexVersion",
                table: "KnowledgeDocuments");
        }
    }
}
