using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.data.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorChunksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-written: EF can't model sqlite-vec virtual tables. vec_chunks lives in vectors.db,
            // attached as "vectors" by SqliteVecConnectionInterceptor. api_key_id is a partition key and
            // group_id a metadata column; never use "+" (auxiliary) columns, they can't be filtered in a KNN
            // query. float[768] must match KnowledgeBase:EmbeddingDimensions.
            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS vectors.vec_chunks USING vec0(
                    embedding float[768] distance_metric=cosine,
                    api_key_id TEXT partition key,
                    group_id TEXT
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS vectors.vec_chunks;");
        }
    }
}
