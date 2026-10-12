namespace api.Data.Models;

public class ApiKey : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public string ResponsibleName { get; set; } = string.Empty;

    public string ContactInfo { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public string KeyPreview { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? RolledOverAt { get; set; }

    public DateTime? LastUsedAt { get; set; }
}
