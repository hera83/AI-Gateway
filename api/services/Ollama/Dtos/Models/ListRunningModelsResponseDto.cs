namespace api.Services.Ollama.Dtos.Models;

public class ListRunningModelsResponseDto
{
    public List<RunningModelDto> Models { get; set; } = [];
}
