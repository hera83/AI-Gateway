namespace api.Dtos.Keys;

public class CreateKeysResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ResponsibleName { get; set; } = string.Empty;

    public string ContactInfo { get; set; } = string.Empty;

    public string KeyPreview { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RolledOverAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    // The raw key. Shown only in this response; it can't be retrieved again, only rolled over.
    public string ApiKey { get; set; } = string.Empty;
}
