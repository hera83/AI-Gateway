using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using api.Services.Ollama.Dtos.Errors;
using api.Services.Ollama.Interfaces;

namespace api.Services.Ollama;

public partial class OllamaService(OllamaHttpClientFactory httpClientFactory) : IOllamaService
{
    // Ollama's wire format is snake_case.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private async Task<TResponse> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken)
        where TResponse : new()
    {
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken) ?? new TResponse();
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest request, CancellationToken cancellationToken)
        where TResponse : new()
    {
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        using var response = await httpClient.PostAsJsonAsync(requestUri, request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken) ?? new TResponse();
    }

    private async Task SendAsync<TRequest>(HttpMethod method, string requestUri, TRequest request, CancellationToken cancellationToken)
    {
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        using var httpRequest = new HttpRequestMessage(method, requestUri)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async IAsyncEnumerable<TResponse> PostStreamAsync<TRequest, TResponse>(
        string requestUri,
        TRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        // ResponseHeadersRead is essential: the default (ResponseContentRead) waits for Ollama's ENTIRE
        // NDJSON body before returning, silently turning streaming into "buffer everything, then dump it".
        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        // Ollama streams newline-delimited JSON (one object per line).
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
        {
            var chunk = JsonSerializer.Deserialize<TResponse>(line, JsonOptions);
            if (chunk is not null)
            {
                yield return chunk;
            }
        }
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
            message = JsonSerializer.Deserialize<ErrorResponseDto>(body, JsonOptions)?.Error ?? body;
        }
        catch (JsonException)
        {
            // Not Ollama's JSON error body, so the raw body is used as-is.
        }

        throw new OllamaException(
            response.StatusCode,
            $"Ollama request to '{response.RequestMessage?.RequestUri}' failed with status {(int)response.StatusCode} ({response.StatusCode}): {message}");
    }
}
