using api.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api.Data.Configurations;

public class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.Property(x => x.OriginalFileName).HasMaxLength(300);
        builder.Property(x => x.RelativePath).HasMaxLength(500);
        builder.Property(x => x.ContentType).HasMaxLength(200);
        // SQLite does not enforce lengths; this documents the expected upper bound for extracted text.
        builder.Property(x => x.ExtractedText).HasMaxLength(100_000_000);
        builder.Property(x => x.MarkdownText).HasMaxLength(100_000_000);
        builder.Property(x => x.MarkdownSource).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.MarkdownLlmDraft).HasMaxLength(100_000_000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(x => x.ApiKeyId);
        builder.HasIndex(x => x.KnowledgeGroupId);
        builder.HasOne<KnowledgeGroup>()
            .WithMany()
            .HasForeignKey(x => x.KnowledgeGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
