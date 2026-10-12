using System.Runtime.CompilerServices;
using api.Services.Ollama.Dtos.Generate;

namespace api.Services.Ollama;

public partial class OllamaService
{
    public Task<GenerateResponseDto> GenerateAsync(GenerateRequestDto request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        return PostAsync<GenerateRequestDto, GenerateResponseDto>("api/generate", request, cancellationToken);
    }

    public async IAsyncEnumerable<GenerateResponseDto> GenerateStreamAsync(
        GenerateRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        await foreach (var chunk in PostStreamAsync<GenerateRequestDto, GenerateResponseDto>("api/generate", request, cancellationToken))
        {
            yield return chunk;
        }
    }
}
