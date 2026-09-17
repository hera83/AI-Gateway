namespace AiGateway.Service.Keys;

public class ApiKeyExpiredException(Guid id, DateTimeOffset expiresAt)
    : Exception($"API key '{id}' expired at {expiresAt:O} and cannot be rolled over. Update its ExpiresAt to a future date first.")
{
    public Guid Id { get; } = id;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;
}
