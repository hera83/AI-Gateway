namespace api.Dtos.Speaches;

public class TranslateSpeachesResponseDto
{
    public string Text { get; set; } = string.Empty;

    public string? Language { get; set; }

    public double? Duration { get; set; }

    public List<TranscriptionSegmentDto>? Segments { get; set; }
}
