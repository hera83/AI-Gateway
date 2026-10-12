using api.Services.Ollama.Dtos.Version;

namespace api.Services.Ollama;

public partial class OllamaService
{
    public Task<VersionResponseDto> GetVersionAsync(CancellationToken cancellationToken = default) =>
        GetAsync<VersionResponseDto>("api/version", cancellationToken);
}
