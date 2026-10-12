using System.Globalization;
using System.Net.Http.Json;
using api.Services.Speaches.Dtos.Audio;

namespace api.Services.Speaches;

public partial class SpeachesService
{
    public Task<TranscriptionResponseDto> TranscribeAsync(TranscriptionRequestDto request, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["model"] = request.Model,
            ["response_format"] = "verbose_json",
            ["stream"] = "false"
        };

        AddIfPresent(fields, "language", request.Language);
        AddIfPresent(fields, "prompt", request.Prompt);
        AddIfPresent(fields, "temperature", request.Temperature);
        AddIfPresent(fields, "hotwords", request.Hotwords);
        if (request.WithoutTimestamps is not null)
        {
            fields["without_timestamps"] = request.WithoutTimestamps.Value ? "true" : "false";
        }

        var content = BuildMultipartContent(fields, request.File, request.FileName, request.ContentType);
        foreach (var granularity in request.TimestampGranularities ?? [])
        {
            content.Add(new StringContent(granularity), "timestamp_granularities");
        }

        return SendMultipartAsync<TranscriptionResponseDto>("v1/audio/transcriptions", content, cancellationToken);
    }

    public Task<TranslationResponseDto> TranslateAsync(TranslationRequestDto request, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["model"] = request.Model,
            ["response_format"] = "verbose_json"
        };

        AddIfPresent(fields, "prompt", request.Prompt);
        AddIfPresent(fields, "temperature", request.Temperature);

        var content = BuildMultipartContent(fields, request.File, request.FileName, request.ContentType);
        return SendMultipartAsync<TranslationResponseDto>("v1/audio/translations", content, cancellationToken);
    }

    public async Task<AudioContentDto> SynthesizeSpeechAsync(SynthesizeRequestDto request, CancellationToken cancellationToken = default)
    {
        request.StreamFormat = "audio";
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        using var response = await httpClient.PostAsJsonAsync("v1/audio/speech", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

        // Speaches returns "audio/mp3", which isn't the registered media type (audio/mpeg). Browsers'
        // <audio> elements, including Swagger UI's player, silently refuse unknown types.
        if (contentType.Equals("audio/mp3", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "audio/mpeg";
        }

        return new AudioContentDto { Content = bytes, ContentType = contentType };
    }

    public Task<SpeechEmbeddingResponseDto> CreateSpeechEmbeddingAsync(SpeechEmbeddingRequestDto request, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string> { ["model"] = request.Model };
        var content = BuildMultipartContent(fields, request.File, request.FileName, request.ContentType);
        return SendMultipartAsync<SpeechEmbeddingResponseDto>("v1/audio/speech/embedding", content, cancellationToken);
    }

    public Task<List<SpeechTimestampDto>> DetectSpeechTimestampsAsync(SpeechTimestampsRequestDto request, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string>();
        AddIfPresent(fields, "model", request.Model);
        AddIfPresent(fields, "threshold", request.Threshold);
        AddIfPresent(fields, "neg_threshold", request.NegThreshold);
        AddIfPresent(fields, "min_speech_duration_ms", request.MinSpeechDurationMs);
        AddIfPresent(fields, "max_speech_duration_s", request.MaxSpeechDurationS);
        AddIfPresent(fields, "min_silence_duration_ms", request.MinSilenceDurationMs);
        AddIfPresent(fields, "speech_pad_ms", request.SpeechPadMs);

        var content = BuildMultipartContent(fields, request.File, request.FileName, request.ContentType);
        return SendMultipartAsync<List<SpeechTimestampDto>>("v1/audio/speech/timestamps", content, cancellationToken);
    }

    public Task<DiarizationResponseDto> DiarizeAsync(DiarizationRequestDto request, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string> { ["response_format"] = "json" };
        var content = BuildMultipartContent(fields, request.File, request.FileName, request.ContentType);

        foreach (var name in request.KnownSpeakerNames ?? [])
        {
            content.Add(new StringContent(name), "known_speaker_names");
        }

        foreach (var reference in request.KnownSpeakerReferences ?? [])
        {
            content.Add(new StringContent(reference), "known_speaker_references");
        }

        return SendMultipartAsync<DiarizationResponseDto>("v1/audio/diarization", content, cancellationToken);
    }

    // Disposes the multipart content (and with it the uploaded file stream) once the call has completed.
    private async Task<TResponse> SendMultipartAsync<TResponse>(string requestUri, MultipartFormDataContent content, CancellationToken cancellationToken)
        where TResponse : new()
    {
        using (content)
        {
            return await PostMultipartAsync<TResponse>(requestUri, content, cancellationToken);
        }
    }

    private static void AddIfPresent(Dictionary<string, string> fields, string name, string? value)
    {
        if (value is not null)
        {
            fields[name] = value;
        }
    }

    private static void AddIfPresent<T>(Dictionary<string, string> fields, string name, T? value)
        where T : struct, IFormattable
    {
        if (value is not null)
        {
            fields[name] = value.Value.ToString(null, CultureInfo.InvariantCulture);
        }
    }
}
