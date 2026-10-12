namespace api.Services.Speaches.Dtos.Models;

public class ListAudioModelsResponseDto
{
    public List<ModelDto> Models { get; set; } = [];

    public string Object { get; set; } = "list";
}
