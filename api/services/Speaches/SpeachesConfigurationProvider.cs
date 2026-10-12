using api.Services.Speaches.Interfaces;

namespace api.Services.Speaches;

public class SpeachesConfigurationProvider(IConfiguration configuration) : ISpeachesConfigurationProvider
{
    public Task<SpeachesSettings> GetActiveConfigurationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(configuration.GetSection("Speaches").Get<SpeachesSettings>() ?? new SpeachesSettings());
}
