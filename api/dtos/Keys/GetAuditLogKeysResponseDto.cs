namespace api.Dtos.Keys;

public class GetAuditLogKeysResponseDto
{
    public string Action { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public string? Details { get; set; }
}
