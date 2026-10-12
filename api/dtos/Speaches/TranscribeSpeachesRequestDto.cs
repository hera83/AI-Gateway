namespace api.Dtos.Speaches;

public class TranscribeSpeachesRequestDto
{
    public string Model { get; set; } = string.Empty;

    public IFormFile File { get; set; } = null!;

    public string? Language { get; set; }

    public string? Prompt { get; set; }

    public double? Temperature { get; set; }

    public List<string>? TimestampGranularities { get; set; }

    public string? Hotwords { get; set; }

    public bool? WithoutTimestamps { get; set; }
}
