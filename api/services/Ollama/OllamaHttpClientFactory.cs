using api.Services.Ollama.Interfaces;

namespace api.Services.Ollama;

public class OllamaHttpClientFactory(IHttpClientFactory httpClientFactory, IOllamaConfigurationProvider configurationProvider)
{
    public async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        var settings = await configurationProvider.GetActiveConfigurationAsync(cancellationToken);
        var client = httpClientFactory.CreateClient("Ollama");
        // Trailing slash so relative paths like "api/chat" are appended instead of replacing the last segment.
        client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromMinutes(settings.TimeoutMinutes);
        return client;
    }
}
