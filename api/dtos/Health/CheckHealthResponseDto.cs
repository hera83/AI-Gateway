namespace api.Dtos.Health;

public class CheckHealthResponseDto
{
    public string Status { get; set; } = string.Empty;

    public List<HealthCheckEntryDto> Checks { get; set; } = [];
}
