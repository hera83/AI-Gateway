namespace api.Repositories.ApiKeys.Dtos;

public class CreateApiKeyDto
{
    public string Name { get; set; } = string.Empty;

    public string ResponsibleName { get; set; } = string.Empty;

    public string ContactInfo { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }

    // Only the hash and preview are stored, never the raw key.
    public string KeyHash { get; set; } = string.Empty;

    public string KeyPreview { get; set; } = string.Empty;
}
