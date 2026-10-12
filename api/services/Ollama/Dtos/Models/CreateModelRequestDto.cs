using System.Text.Json;
using api.Services.Ollama.Dtos.Common;

namespace api.Services.Ollama.Dtos.Models;

public class CreateModelRequestDto
{
    public string Model { get; set; } = string.Empty;

    public string? From { get; set; }

    public Dictionary<string, string>? Files { get; set; }

    public Dictionary<string, string>? Adapters { get; set; }

    public string? Template { get; set; }

    public string? License { get; set; }

    public string? System { get; set; }

    public Dictionary<string, JsonElement>? Parameters { get; set; }

    public List<MessageDto>? Messages { get; set; }

    public string? Quantize { get; set; }

    public bool Stream { get; set; }
}
