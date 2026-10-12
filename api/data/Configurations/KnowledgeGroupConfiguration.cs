using api.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api.Data.Configurations;

public class KnowledgeGroupConfiguration : IEntityTypeConfiguration<KnowledgeGroup>
{
    public void Configure(EntityTypeBuilder<KnowledgeGroup> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.HasIndex(x => new { x.ApiKeyId, x.Name }).IsUnique();
        builder.HasOne<ApiKey>()
            .WithMany()
            .HasForeignKey(x => x.ApiKeyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
