namespace api.Services.Speaches.Dtos.Audio;

public class TranslationResponseDto
{
    public string Text { get; set; } = string.Empty;

    public string? Language { get; set; }

    public double? Duration { get; set; }

    public List<TranscriptionSegmentDto>? Segments { get; set; }
}
