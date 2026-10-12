using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using api.Services.Speaches.Dtos.Errors;
using api.Services.Speaches.Interfaces;

namespace api.Services.Speaches;

public partial class SpeachesService(SpeachesHttpClientFactory httpClientFactory, ILogger<SpeachesService> logger) : ISpeachesService
{
    // Speaches' OpenAI-compatible wire format is snake_case. Unset optional fields are left out instead of sent
    // as null, because Speaches rejects null for fields that have a default (e.g. "speed" on v1/audio/speech → 422).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private async Task<TResponse> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken)
        where TResponse : new()
    {
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadJsonOrDefaultAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(string requestUri, TRequest request, CancellationToken cancellationToken)
        where TResponse : new()
    {
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        logger.LogInformation("Sending request to Speaches at '{RequestUri}'.", requestUri);
        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.PostAsJsonAsync(requestUri, request, JsonOptions, cancellationToken);
        logger.LogInformation(
            "Received response from Speaches for '{RequestUri}' after {ElapsedMilliseconds} ms (status {StatusCode}).",
            requestUri, stopwatch.ElapsedMilliseconds, (int)response.StatusCode);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadJsonOrDefaultAsync<TResponse>(response, cancellationToken);
    }

    // Long-running (large audio uploads can take minutes), so elapsed time is logged at both ends: there is
    // no other progress signal between the request starting and ending.
    private async Task<TResponse> PostMultipartAsync<TResponse>(string requestUri, MultipartFormDataContent content, CancellationToken cancellationToken)
        where TResponse : new()
    {
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        logger.LogInformation(
            "Sending multipart request to Speaches at '{RequestUri}' ({ContentLength} bytes).",
            requestUri, content.Headers.ContentLength?.ToString(CultureInfo.InvariantCulture) ?? "unknown");
        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
        logger.LogInformation(
            "Received response from Speaches for '{RequestUri}' after {ElapsedMilliseconds} ms (status {StatusCode}).",
            requestUri, stopwatch.ElapsedMilliseconds, (int)response.StatusCode);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadJsonOrDefaultAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse> SendWithoutBodyAsync<TResponse>(HttpMethod method, string requestUri, CancellationToken cancellationToken)
        where TResponse : new()
    {
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        using var httpRequest = new HttpRequestMessage(method, requestUri);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadJsonOrDefaultAsync<TResponse>(response, cancellationToken);
    }

    // For calls whose answer isn't used. The body is never parsed, because Speaches answers some of them with
    // plain text instead of JSON (POST v1/models/{id}: "Model '...' downloaded").
    private async Task SendWithoutBodyAsync(HttpMethod method, string requestUri, CancellationToken cancellationToken)
    {
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        using var httpRequest = new HttpRequestMessage(method, requestUri);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static MultipartFormDataContent BuildMultipartContent(IDictionary<string, string> fields, Stream file, string fileName, string? contentType)
    {
        var content = new MultipartFormDataContent();
        foreach (var (key, value) in fields)
        {
            content.Add(new StringContent(value), key);
        }

        var fileContent = new StreamContent(file);
        if (!string.IsNullOrEmpty(contentType))
        {
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        content.Add(fileContent, "file", fileName);
        return content;
    }

    // Some Speaches endpoints answer 2xx with an empty body.
    private static async Task<TResponse> ReadJsonOrDefaultAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
        where TResponse : new()
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body)
            ? new TResponse()
            : JsonSerializer.Deserialize<TResponse>(body, JsonOptions) ?? new TResponse();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = body;
        try
        {
            var error = JsonSerializer.Deserialize<ErrorResponseDto>(body, JsonOptions);
            var element = error?.Detail ?? error?.Error;
            if (element is { } value)
            {
                message = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
            }
        }
        catch (JsonException)
        {
            // Not a JSON error body, so the raw body is used as-is.
        }

        // Only the path, never the host: the message is sent to API clients as "detail", and BaseUrl is an internal address.
        throw new SpeachesException(
            response.StatusCode,
            $"Speaches request to '{response.RequestMessage?.RequestUri?.PathAndQuery}' failed with status {(int)response.StatusCode} ({response.StatusCode}): {message}");
    }
}
