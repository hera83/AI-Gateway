using System.Net.WebSockets;
using api.Services.Speaches.Dtos.Audio;
using api.Services.Speaches.Dtos.Chat;
using api.Services.Speaches.Dtos.Models;

namespace api.Services.Speaches.Interfaces;

public interface ISpeachesService
{
    Task<ChatCompletionResponseDto> ChatCompletionsAsync(ChatCompletionRequestDto request, CancellationToken cancellationToken = default);

    Task<TranscriptionResponseDto> TranscribeAsync(TranscriptionRequestDto request, CancellationToken cancellationToken = default);

    // Proxies an already-accepted client WebSocket to Speaches' /v1/realtime (intent=transcription): a
    // duplex relay, not a request/response call, until either side closes the connection.
    Task ProxyRealtimeTranscriptionAsync(WebSocket clientSocket, string model, string? language, CancellationToken cancellationToken = default);

    Task<TranslationResponseDto> TranslateAsync(TranslationRequestDto request, CancellationToken cancellationToken = default);

    Task<AudioContentDto> SynthesizeSpeechAsync(SynthesizeRequestDto request, CancellationToken cancellationToken = default);

    Task<SpeechEmbeddingResponseDto> CreateSpeechEmbeddingAsync(SpeechEmbeddingRequestDto request, CancellationToken cancellationToken = default);

    Task<List<SpeechTimestampDto>> DetectSpeechTimestampsAsync(SpeechTimestampsRequestDto request, CancellationToken cancellationToken = default);

    Task<DiarizationResponseDto> DiarizeAsync(DiarizationRequestDto request, CancellationToken cancellationToken = default);

    Task<ListModelsResponseDto> ListModelsAsync(string? task, CancellationToken cancellationToken = default);

    Task<ListAudioModelsResponseDto> ListAudioModelsAsync(CancellationToken cancellationToken = default);

    Task<ListModelsResponseDto> ListVoicesAsync(CancellationToken cancellationToken = default);

    Task<ModelDto> GetModelAsync(string modelId, CancellationToken cancellationToken = default);

    Task DownloadModelAsync(string modelId, CancellationToken cancellationToken = default);

    Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default);

    Task<ListModelsResponseDto> GetRegistryAsync(string? task, CancellationToken cancellationToken = default);

    Task<RunningModelsResponseDto> ListRunningModelsAsync(CancellationToken cancellationToken = default);

    Task<MessageResponseDto> LoadModelAsync(string modelId, CancellationToken cancellationToken = default);

    Task<MessageResponseDto> StopModelAsync(string modelId, CancellationToken cancellationToken = default);
}
