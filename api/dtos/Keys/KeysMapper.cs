using api.Extensions;
using api.Repositories.ApiKeys.Dtos;

namespace api.Dtos.Keys;

public static class KeysMapper
{
    public static CreateApiKeyDto ToCreateApiKeyDto(this CreateKeysRequestDto request, string keyHash, string keyPreview) => new()
    {
        Name = request.Name,
        ResponsibleName = request.ResponsibleName,
        ContactInfo = request.ContactInfo,
        ExpiresAt = request.ExpiresAt.AsUtc(),
        KeyHash = keyHash,
        KeyPreview = keyPreview
    };

    public static UpdateApiKeyDto ToUpdateApiKeyDto(this UpdateKeysRequestDto request) => new()
    {
        Name = request.Name,
        ResponsibleName = request.ResponsibleName,
        ContactInfo = request.ContactInfo,
        ExpiresAt = request.ExpiresAt.AsUtc()
    };

    public static CreateKeysResponseDto ToCreateKeysResponseDto(this ApiKeyDto key, string apiKey) => new()
    {
        Id = key.Id,
        Name = key.Name,
        ResponsibleName = key.ResponsibleName,
        ContactInfo = key.ContactInfo,
        KeyPreview = key.KeyPreview,
        IsActive = key.IsActive,
        ExpiresAt = key.ExpiresAt,
        CreatedAt = key.CreatedAt,
        RolledOverAt = key.RolledOverAt,
        LastUsedAt = key.LastUsedAt,
        ApiKey = apiKey
    };

    public static ListKeysResponseDto ToListKeysResponseDto(this ApiKeyDto key) => new()
    {
        Id = key.Id,
        Name = key.Name,
        ResponsibleName = key.ResponsibleName,
        ContactInfo = key.ContactInfo,
        KeyPreview = key.KeyPreview,
        IsActive = key.IsActive,
        ExpiresAt = key.ExpiresAt,
        CreatedAt = key.CreatedAt,
        RolledOverAt = key.RolledOverAt,
        LastUsedAt = key.LastUsedAt
    };

    public static GetByIdKeysResponseDto ToGetByIdKeysResponseDto(this ApiKeyDto key) => new()
    {
        Id = key.Id,
        Name = key.Name,
        ResponsibleName = key.ResponsibleName,
        ContactInfo = key.ContactInfo,
        KeyPreview = key.KeyPreview,
        IsActive = key.IsActive,
        ExpiresAt = key.ExpiresAt,
        CreatedAt = key.CreatedAt,
        RolledOverAt = key.RolledOverAt,
        LastUsedAt = key.LastUsedAt
    };

    public static UpdateKeysResponseDto ToUpdateKeysResponseDto(this ApiKeyDto key) => new()
    {
        Id = key.Id,
        Name = key.Name,
        ResponsibleName = key.ResponsibleName,
        ContactInfo = key.ContactInfo,
        KeyPreview = key.KeyPreview,
        IsActive = key.IsActive,
        ExpiresAt = key.ExpiresAt,
        CreatedAt = key.CreatedAt,
        RolledOverAt = key.RolledOverAt,
        LastUsedAt = key.LastUsedAt
    };

    public static RolloverKeysResponseDto ToRolloverKeysResponseDto(this ApiKeyDto key, string apiKey) => new()
    {
        Id = key.Id,
        Name = key.Name,
        ResponsibleName = key.ResponsibleName,
        ContactInfo = key.ContactInfo,
        KeyPreview = key.KeyPreview,
        IsActive = key.IsActive,
        ExpiresAt = key.ExpiresAt,
        CreatedAt = key.CreatedAt,
        RolledOverAt = key.RolledOverAt,
        LastUsedAt = key.LastUsedAt,
        ApiKey = apiKey
    };

    public static ActivateKeysResponseDto ToActivateKeysResponseDto(this ApiKeyDto key) => new()
    {
        Id = key.Id,
        Name = key.Name,
        ResponsibleName = key.ResponsibleName,
        ContactInfo = key.ContactInfo,
        KeyPreview = key.KeyPreview,
        IsActive = key.IsActive,
        ExpiresAt = key.ExpiresAt,
        CreatedAt = key.CreatedAt,
        RolledOverAt = key.RolledOverAt,
        LastUsedAt = key.LastUsedAt
    };

    public static DeactivateKeysResponseDto ToDeactivateKeysResponseDto(this ApiKeyDto key) => new()
    {
        Id = key.Id,
        Name = key.Name,
        ResponsibleName = key.ResponsibleName,
        ContactInfo = key.ContactInfo,
        KeyPreview = key.KeyPreview,
        IsActive = key.IsActive,
        ExpiresAt = key.ExpiresAt,
        CreatedAt = key.CreatedAt,
        RolledOverAt = key.RolledOverAt,
        LastUsedAt = key.LastUsedAt
    };

    public static GetAuditLogKeysResponseDto ToGetAuditLogKeysResponseDto(this ApiKeyAuditLogDto entry) => new()
    {
        Action = entry.Action,
        Timestamp = entry.CreatedAt,
        Details = entry.Details
    };
}
