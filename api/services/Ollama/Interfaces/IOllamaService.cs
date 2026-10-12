using api.Services.Ollama.Dtos.Chat;
using api.Services.Ollama.Dtos.Embed;
using api.Services.Ollama.Dtos.Generate;
using api.Services.Ollama.Dtos.Models;
using api.Services.Ollama.Dtos.Version;

namespace api.Services.Ollama.Interfaces;

public interface IOllamaService
{
    Task<GenerateResponseDto> GenerateAsync(GenerateRequestDto request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<GenerateResponseDto> GenerateStreamAsync(GenerateRequestDto request, CancellationToken cancellationToken = default);

    Task<ChatResponseDto> ChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatResponseDto> ChatStreamAsync(ChatRequestDto request, CancellationToken cancellationToken = default);

    Task<EmbedResponseDto> EmbedAsync(EmbedRequestDto request, CancellationToken cancellationToken = default);

    Task<ListModelsResponseDto> ListModelsAsync(CancellationToken cancellationToken = default);

    Task<ListRunningModelsResponseDto> ListRunningModelsAsync(CancellationToken cancellationToken = default);

    Task<ShowModelResponseDto> ShowModelAsync(ShowModelRequestDto request, CancellationToken cancellationToken = default);

    Task CopyModelAsync(CopyModelRequestDto request, CancellationToken cancellationToken = default);

    Task DeleteModelAsync(DeleteModelRequestDto request, CancellationToken cancellationToken = default);

    Task<PullModelResponseDto> PullModelAsync(PullModelRequestDto request, CancellationToken cancellationToken = default);

    Task<PushModelResponseDto> PushModelAsync(PushModelRequestDto request, CancellationToken cancellationToken = default);

    Task<CreateModelResponseDto> CreateModelAsync(CreateModelRequestDto request, CancellationToken cancellationToken = default);

    Task<VersionResponseDto> GetVersionAsync(CancellationToken cancellationToken = default);
}
