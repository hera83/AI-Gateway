namespace api.Repositories.ApiKeys.Dtos;

public class ApiKeyAuditLogDto
{
    public string Action { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string? Details { get; set; }
}
