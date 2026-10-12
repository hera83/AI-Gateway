using api.Services.Speaches.Dtos.Models;

namespace api.Services.Speaches;

public partial class SpeachesService
{
    public Task<ListModelsResponseDto> ListModelsAsync(string? task, CancellationToken cancellationToken = default) =>
        GetAsync<ListModelsResponseDto>(task is null ? "v1/models" : $"v1/models?task={Uri.EscapeDataString(task)}", cancellationToken);

    public Task<ListAudioModelsResponseDto> ListAudioModelsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ListAudioModelsResponseDto>("v1/audio/models", cancellationToken);

    public Task<ListModelsResponseDto> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ListModelsResponseDto>("v1/audio/voices", cancellationToken);

    public Task<ModelDto> GetModelAsync(string modelId, CancellationToken cancellationToken = default) =>
        GetAsync<ModelDto>($"v1/models/{modelId}", cancellationToken);

    public Task DownloadModelAsync(string modelId, CancellationToken cancellationToken = default) =>
        SendWithoutBodyAsync<MessageResponseDto>(HttpMethod.Post, $"v1/models/{modelId}", cancellationToken);

    public Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default) =>
        SendWithoutBodyAsync<MessageResponseDto>(HttpMethod.Delete, $"v1/models/{modelId}", cancellationToken);

    public Task<ListModelsResponseDto> GetRegistryAsync(string? task, CancellationToken cancellationToken = default) =>
        GetAsync<ListModelsResponseDto>(task is null ? "v1/registry" : $"v1/registry?task={Uri.EscapeDataString(task)}", cancellationToken);

    public Task<RunningModelsResponseDto> ListRunningModelsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<RunningModelsResponseDto>("api/ps", cancellationToken);

    public Task<MessageResponseDto> LoadModelAsync(string modelId, CancellationToken cancellationToken = default) =>
        SendWithoutBodyAsync<MessageResponseDto>(HttpMethod.Post, $"api/ps/{modelId}", cancellationToken);

    public Task<MessageResponseDto> StopModelAsync(string modelId, CancellationToken cancellationToken = default) =>
        SendWithoutBodyAsync<MessageResponseDto>(HttpMethod.Delete, $"api/ps/{modelId}", cancellationToken);
}
