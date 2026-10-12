namespace api.Services.Ollama.Dtos.Models;

public class ListModelsResponseDto
{
    public List<ModelSummaryDto> Models { get; set; } = [];
}
