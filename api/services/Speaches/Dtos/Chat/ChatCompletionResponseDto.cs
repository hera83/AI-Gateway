namespace api.Services.Speaches.Dtos.Chat;

public class ChatCompletionResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string Object { get; set; } = string.Empty;

    public long Created { get; set; }

    public string Model { get; set; } = string.Empty;

    public List<ChatCompletionChoiceDto> Choices { get; set; } = [];

    public ChatCompletionUsageDto? Usage { get; set; }

    public string? SystemFingerprint { get; set; }

    public string? ServiceTier { get; set; }
}
