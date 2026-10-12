namespace api.Dtos.Speaches;

public class TranscribeRealtimeSpeachesRequestDto
{
    public string Model { get; set; } = string.Empty;

    public string? Language { get; set; }
}
