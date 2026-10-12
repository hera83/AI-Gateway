using api.Data;
using api.Data.Models;
using api.Repositories.ApiKeys.Dtos;
using api.Repositories.ApiKeys.Interfaces;
using api.Repositories.ApiKeys.Mappers;
using Microsoft.EntityFrameworkCore;

namespace api.Repositories.ApiKeys;

public class ApiKeyRepository(ApiDbContext dbContext) : IApiKeyRepository
{
    public async Task<ApiKeyDto> CreateAsync(CreateApiKeyDto dto, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity();
        dbContext.ApiKeys.Add(entity);
        AddAuditLog(entity.Id, ApiKeyAuditAction.Created, $"Nøgle '{entity.Name}' oprettet.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public Task<List<ApiKeyDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        dbContext.ApiKeys.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(ApiKeyMapper.ToDtoExpression)
            .ToListAsync(cancellationToken);

    public Task<ApiKeyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ApiKeys.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ApiKeyMapper.ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ApiKeyDto?> GetValidByHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return dbContext.ApiKeys.AsNoTracking()
            .Where(x => x.KeyHash == keyHash && x.IsActive && (x.ExpiresAt == null || x.ExpiresAt > now))
            .Select(ApiKeyMapper.ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ApiKeyDto?> UpdateAsync(Guid id, UpdateApiKeyDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ApiKeys.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        dto.ApplyTo(entity);
        AddAuditLog(entity.Id, ApiKeyAuditAction.Updated, $"Nøgle '{entity.Name}' opdateret.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<ApiKeyDto?> RolloverAsync(Guid id, string keyHash, string keyPreview, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ApiKeys.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        entity.KeyHash = keyHash;
        entity.KeyPreview = keyPreview;
        entity.RolledOverAt = now;

        // Rollover doubles as "renew": rotating an expired key's secret also makes it usable again,
        // instead of handing back a raw key that can never authenticate.
        entity.ExpiresAt = now.AddYears(1);

        AddAuditLog(entity.Id, ApiKeyAuditAction.RolledOver, $"Nøgle '{entity.Name}' roteret, ny udløbsdato: {entity.ExpiresAt:yyyy-MM-dd}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<ApiKeyDto?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ApiKeys.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.IsActive = isActive;

        // Activate doubles as "renew" (ExpiresAt a year out), and deactivate sets ExpiresAt to now, so a
        // deactivated key also reads as expired everywhere.
        if (isActive)
        {
            entity.ExpiresAt = DateTime.UtcNow.AddYears(1);
            AddAuditLog(entity.Id, ApiKeyAuditAction.Activated, $"Nøgle '{entity.Name}' aktiveret, ny udløbsdato: {entity.ExpiresAt:yyyy-MM-dd}.");
        }
        else
        {
            entity.ExpiresAt = DateTime.UtcNow;
            AddAuditLog(entity.Id, ApiKeyAuditAction.Deactivated, $"Nøgle '{entity.Name}' deaktiveret.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<List<ApiKeyAuditLogDto>?> GetAuditLogAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.ApiKeys.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return null;
        }

        return await dbContext.ApiKeyAuditLogs.AsNoTracking()
            .Where(x => x.ApiKeyId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(ApiKeyAuditLogMapper.ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    // Called on every authenticated request, so it updates the column directly instead of loading the row.
    public Task MarkUsedAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ApiKeys
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LastUsedAt, DateTime.UtcNow), cancellationToken);

    // Hard delete. The database cascades to the key's audit log and its knowledge base rows.
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.ApiKeys.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    private void AddAuditLog(Guid apiKeyId, ApiKeyAuditAction action, string details) =>
        dbContext.ApiKeyAuditLogs.Add(ApiKeyAuditLogMapper.ToEntity(apiKeyId, action, details));
}
