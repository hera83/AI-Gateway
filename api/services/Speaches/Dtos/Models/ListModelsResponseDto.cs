namespace api.Services.Speaches.Dtos.Models;

public class ListModelsResponseDto
{
    public List<ModelDto> Data { get; set; } = [];

    public string Object { get; set; } = "list";
}
