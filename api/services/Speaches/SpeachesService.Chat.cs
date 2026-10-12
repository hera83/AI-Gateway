using api.Services.Speaches.Dtos.Chat;

namespace api.Services.Speaches;

public partial class SpeachesService
{
    public Task<ChatCompletionResponseDto> ChatCompletionsAsync(ChatCompletionRequestDto request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        return PostJsonAsync<ChatCompletionRequestDto, ChatCompletionResponseDto>("v1/chat/completions", request, cancellationToken);
    }
}
