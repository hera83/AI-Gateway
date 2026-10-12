namespace api.Services.Speaches.Interfaces;

public interface ISpeachesConfigurationProvider
{
    Task<SpeachesSettings> GetActiveConfigurationAsync(CancellationToken cancellationToken = default);
}
