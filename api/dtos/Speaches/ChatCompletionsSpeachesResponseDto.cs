namespace api.Dtos.Speaches;

public class ChatCompletionsSpeachesResponseDto
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
