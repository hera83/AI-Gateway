namespace api.Dtos.Speaches;

public class CreateSpeechEmbeddingSpeachesRequestDto
{
    public string Model { get; set; } = string.Empty;

    public IFormFile File { get; set; } = null!;
}
