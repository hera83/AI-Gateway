namespace api.Repositories.ApiKeys.Dtos;

public class UpdateApiKeyDto
{
    public string Name { get; set; } = string.Empty;

    public string ResponsibleName { get; set; } = string.Empty;

    public string ContactInfo { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }
}
