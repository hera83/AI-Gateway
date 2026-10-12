namespace api.Services.Speaches.Dtos.Audio;

public class SpeechEmbeddingResponseDto
{
    public string Object { get; set; } = "list";

    public List<EmbeddingObjectDto> Data { get; set; } = [];

    public string Model { get; set; } = string.Empty;

    public EmbeddingUsageDto Usage { get; set; } = new();
}
