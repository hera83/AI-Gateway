using System.Runtime.CompilerServices;
using api.Services.Ollama.Dtos.Chat;

namespace api.Services.Ollama;

public partial class OllamaService
{
    public Task<ChatResponseDto> ChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        return PostAsync<ChatRequestDto, ChatResponseDto>("api/chat", request, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseDto> ChatStreamAsync(
        ChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        await foreach (var chunk in PostStreamAsync<ChatRequestDto, ChatResponseDto>("api/chat", request, cancellationToken))
        {
            yield return chunk;
        }
    }
}
