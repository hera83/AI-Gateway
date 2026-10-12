using System.Text.Json;

namespace api.Dtos.Speaches;

public class ChatCompletionChoiceDto
{
    public int Index { get; set; }

    public ChatCompletionResponseMessageDto Message { get; set; } = new();

    public string? FinishReason { get; set; }

    public JsonElement? Logprobs { get; set; }
}
