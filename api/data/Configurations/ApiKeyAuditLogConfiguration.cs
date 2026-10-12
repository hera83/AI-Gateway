using api.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api.Data.Configurations;

public class ApiKeyAuditLogConfiguration : IEntityTypeConfiguration<ApiKeyAuditLog>
{
    public void Configure(EntityTypeBuilder<ApiKeyAuditLog> builder)
    {
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Details).HasMaxLength(1000);
        builder.HasIndex(x => x.ApiKeyId);
        builder.HasOne<ApiKey>()
            .WithMany()
            .HasForeignKey(x => x.ApiKeyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
