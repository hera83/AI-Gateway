using api.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.ResponsibleName).HasMaxLength(200);
        builder.Property(x => x.ContactInfo).HasMaxLength(300);
        builder.Property(x => x.KeyHash).HasMaxLength(64);
        builder.Property(x => x.KeyPreview).HasMaxLength(20);
        builder.HasIndex(x => x.KeyHash).IsUnique();
    }
}
