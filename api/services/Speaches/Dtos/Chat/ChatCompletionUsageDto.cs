using System.Text.Json;

namespace api.Services.Speaches.Dtos.Chat;

public class ChatCompletionUsageDto
{
    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public JsonElement? PromptTokensDetails { get; set; }

    public JsonElement? CompletionTokensDetails { get; set; }
}
