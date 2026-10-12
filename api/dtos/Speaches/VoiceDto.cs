namespace api.Dtos.Speaches;

public class VoiceDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Language { get; set; }

    public string? Gender { get; set; }
}
