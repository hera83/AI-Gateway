using System.Text.Json;

namespace api.Services.Speaches.Dtos.Chat;

public class ChatCompletionRequestDto
{
    public string Model { get; set; } = string.Empty;

    public List<ChatCompletionMessageDto> Messages { get; set; } = [];

    public double? Temperature { get; set; }

    public double? TopP { get; set; }

    public int? MaxCompletionTokens { get; set; }

    public int? N { get; set; }

    public JsonElement? Stop { get; set; }

    public double? PresencePenalty { get; set; }

    public double? FrequencyPenalty { get; set; }

    public int? Seed { get; set; }

    public string? User { get; set; }

    public JsonElement? Tools { get; set; }

    public JsonElement? ToolChoice { get; set; }

    public JsonElement? ResponseFormat { get; set; }

    public bool? Logprobs { get; set; }

    public int? TopLogprobs { get; set; }

    public bool Stream { get; set; }
}
