namespace api.Services.Speaches.Dtos.Audio;

public class SpeechEmbeddingRequestDto
{
    public string Model { get; set; } = string.Empty;

    public Stream File { get; set; } = Stream.Null;

    public string FileName { get; set; } = string.Empty;

    public string? ContentType { get; set; }
}
