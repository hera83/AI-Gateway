using api.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api.Data.Configurations;

public class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        // SQLite does not enforce lengths; the real chunk size comes from KnowledgeBase:ChunkSizeCharacters.
        builder.Property(x => x.Text).HasMaxLength(100_000);
        builder.HasIndex(x => x.ApiKeyId);
        builder.HasIndex(x => x.KnowledgeDocumentId);
        builder.HasIndex(x => x.VectorRowId).IsUnique();
        builder.HasOne<KnowledgeDocument>()
            .WithMany()
            .HasForeignKey(x => x.KnowledgeDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
