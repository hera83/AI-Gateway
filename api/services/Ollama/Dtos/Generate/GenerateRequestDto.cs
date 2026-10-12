using System.Text.Json;
using api.Services.Ollama.Dtos.Common;

namespace api.Services.Ollama.Dtos.Generate;

public class GenerateRequestDto
{
    public string Model { get; set; } = string.Empty;

    public string? Prompt { get; set; }

    public string? Suffix { get; set; }

    public List<string>? Images { get; set; }

    public JsonElement? Format { get; set; }

    public OptionsDto? Options { get; set; }

    public string? System { get; set; }

    public string? Template { get; set; }

    public List<long>? Context { get; set; }

    public bool? Raw { get; set; }

    public string? KeepAlive { get; set; }

    public bool Stream { get; set; }
}
