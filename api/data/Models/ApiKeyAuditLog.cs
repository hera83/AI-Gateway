namespace api.Data.Models;

public class ApiKeyAuditLog : EntityBase
{
    public Guid ApiKeyId { get; set; }

    public ApiKeyAuditAction Action { get; set; }

    public string? Details { get; set; }
}
