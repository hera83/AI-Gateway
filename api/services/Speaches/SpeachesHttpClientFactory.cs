using api.Services.Speaches.Interfaces;

namespace api.Services.Speaches;

public class SpeachesHttpClientFactory(IHttpClientFactory httpClientFactory, ISpeachesConfigurationProvider configurationProvider)
{
    public async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        var settings = await configurationProvider.GetActiveConfigurationAsync(cancellationToken);
        var client = httpClientFactory.CreateClient("Speaches");
        // Trailing slash so relative paths like "v1/models" are appended instead of replacing the last segment.
        client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromMinutes(settings.TimeoutMinutes);
        return client;
    }
}
