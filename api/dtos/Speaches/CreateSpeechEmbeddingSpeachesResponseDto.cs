namespace api.Dtos.Speaches;

public class CreateSpeechEmbeddingSpeachesResponseDto
{
    public string Object { get; set; } = "list";

    public List<EmbeddingObjectDto> Data { get; set; } = [];

    public string Model { get; set; } = string.Empty;

    public EmbeddingUsageDto Usage { get; set; } = new();
}
