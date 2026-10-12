using api.Services.Ollama.Interfaces;

namespace api.Services.Ollama;

public class OllamaConfigurationProvider(IConfiguration configuration) : IOllamaConfigurationProvider
{
    public Task<OllamaSettings> GetActiveConfigurationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(configuration.GetSection("Ollama").Get<OllamaSettings>() ?? new OllamaSettings());
}
