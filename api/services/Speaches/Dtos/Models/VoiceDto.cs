namespace api.Services.Speaches.Dtos.Models;

public class VoiceDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Language { get; set; }

    // Only some voice models (e.g. Kokoro) report it.
    public string? Gender { get; set; }
}
