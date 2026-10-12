using System.Text.Json;

namespace api.Dtos.Speaches;

public class ChatCompletionResponseMessageDto
{
    public string Role { get; set; } = string.Empty;

    public string? Content { get; set; }

    public string? Refusal { get; set; }

    public JsonElement? ToolCalls { get; set; }

    public JsonElement? Audio { get; set; }

    public JsonElement? Annotations { get; set; }
}
