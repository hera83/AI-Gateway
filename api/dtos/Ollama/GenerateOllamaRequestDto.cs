using System.Text.Json;

namespace api.Dtos.Ollama;

public class GenerateOllamaRequestDto
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

    // Defaults to false (unlike Ollama's own default of true) so existing callers that don't set
    // this keep getting today's single-JSON-object response instead of suddenly switching shape.
    public bool Stream { get; set; }
}
