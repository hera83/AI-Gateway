using api.Services.Ollama.Dtos.Models;

namespace api.Services.Ollama;

public partial class OllamaService
{
    public Task<ListModelsResponseDto> ListModelsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ListModelsResponseDto>("api/tags", cancellationToken);

    public Task<ListRunningModelsResponseDto> ListRunningModelsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ListRunningModelsResponseDto>("api/ps", cancellationToken);

    public Task<ShowModelResponseDto> ShowModelAsync(ShowModelRequestDto request, CancellationToken cancellationToken = default) =>
        PostAsync<ShowModelRequestDto, ShowModelResponseDto>("api/show", request, cancellationToken);

    public Task CopyModelAsync(CopyModelRequestDto request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "api/copy", request, cancellationToken);

    public Task DeleteModelAsync(DeleteModelRequestDto request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, "api/delete", request, cancellationToken);

    public Task<PullModelResponseDto> PullModelAsync(PullModelRequestDto request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        return PostAsync<PullModelRequestDto, PullModelResponseDto>("api/pull", request, cancellationToken);
    }

    public Task<PushModelResponseDto> PushModelAsync(PushModelRequestDto request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        return PostAsync<PushModelRequestDto, PushModelResponseDto>("api/push", request, cancellationToken);
    }

    public Task<CreateModelResponseDto> CreateModelAsync(CreateModelRequestDto request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        return PostAsync<CreateModelRequestDto, CreateModelResponseDto>("api/create", request, cancellationToken);
    }
}
