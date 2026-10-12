using System.Linq.Expressions;
using api.Data.Models;
using api.Repositories.ApiKeys.Dtos;

namespace api.Repositories.ApiKeys.Mappers;

public static class ApiKeyAuditLogMapper
{
    public static readonly Expression<Func<ApiKeyAuditLog, ApiKeyAuditLogDto>> ToDtoExpression = x => new ApiKeyAuditLogDto
    {
        Action = x.Action.ToString(),
        CreatedAt = x.CreatedAt,
        Details = x.Details
    };

    public static ApiKeyAuditLog ToEntity(Guid apiKeyId, ApiKeyAuditAction action, string details) => new()
    {
        ApiKeyId = apiKeyId,
        Action = action,
        Details = details
    };
}
