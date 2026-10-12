using api.Services.Ollama.Dtos.Embed;

namespace api.Services.Ollama;

public partial class OllamaService
{
    public Task<EmbedResponseDto> EmbedAsync(EmbedRequestDto request, CancellationToken cancellationToken = default) =>
        PostAsync<EmbedRequestDto, EmbedResponseDto>("api/embed", request, cancellationToken);
}
