namespace api.Managers.ApiKeys.Interfaces;

public interface IApiKeyManager
{
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
