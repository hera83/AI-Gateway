using System.Data;
using System.Text.Json;
using api.Data;
using api.Data.Models;
using api.Repositories.KnowledgeDocuments.Dtos;
using api.Repositories.KnowledgeDocuments.Interfaces;
using api.Repositories.KnowledgeDocuments.Mappers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace api.Repositories.KnowledgeDocuments;

// Owns KnowledgeDocuments + KnowledgeChunks (app.db), vec_chunks (vectors.db, attached as "vectors" by
// SqliteVecConnectionInterceptor) and the original uploads in app_files/KnowledgeDocuments/.
// Every query is scoped to the calling key's own ApiKeyId.
public class KnowledgeDocumentRepository(
    ApiDbContext dbContext,
    IHostEnvironment environment,
    ILogger<KnowledgeDocumentRepository> logger) : IKnowledgeDocumentRepository
{
    private const string FilesFolder = "KnowledgeDocuments";
    private const int MaxErrorMessageLength = 2000;

    public async Task<KnowledgeDocumentDto> CreateAsync(CreateKnowledgeDocumentDto dto, CancellationToken cancellationToken = default)
    {
        var relativePath = $"{FilesFolder}/{Guid.NewGuid():N}{Path.GetExtension(dto.OriginalFileName).ToLowerInvariant()}";
        var absolutePath = GetAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        // File first, then the row. If the row can't be saved, the file is removed again.
        await using (var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write))
        {
            await dto.Content.CopyToAsync(fileStream, cancellationToken);
        }

        var entity = dto.ToEntity(relativePath);
        dbContext.KnowledgeDocuments.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            TryDeleteFile(relativePath);
            throw;
        }

        return entity.ToDto();
    }

    public Task<List<KnowledgeDocumentDto>> GetAllAsync(Guid apiKeyId, Guid? knowledgeGroupId, CancellationToken cancellationToken = default) =>
        Filter(apiKeyId, knowledgeGroupId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(KnowledgeDocumentMapper.ToDtoExpression)
            .ToListAsync(cancellationToken);

    public Task<List<Guid>> GetIdsAsync(Guid apiKeyId, Guid? knowledgeGroupId, CancellationToken cancellationToken = default) =>
        Filter(apiKeyId, knowledgeGroupId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<KnowledgeDocumentFileDto?> GetFileAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId && x.Id == id)
            .Select(x => new { x.RelativePath, x.OriginalFileName, x.ContentType })
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return null;
        }

        var absolutePath = GetAbsolutePath(document.RelativePath);
        return File.Exists(absolutePath)
            ? KnowledgeDocumentMapper.ToFileDto(absolutePath, document.OriginalFileName, document.ContentType)
            : null;
    }

    public async Task<Stream?> OpenFileAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default)
    {
        var file = await GetFileAsync(apiKeyId, id, cancellationToken);
        return file is null
            ? null
            : new FileStream(file.AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
    }

    public async Task<KnowledgeDocumentDto?> MarkFailedAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Status = KnowledgeDocumentStatus.Failed;
        entity.ErrorMessage = errorMessage.Length > MaxErrorMessageLength ? errorMessage[..MaxErrorMessageLength] : errorMessage;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<KnowledgeDocumentDto?> MarkIndexedAsync(Guid id, IndexKnowledgeDocumentDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var connection = await GetOpenConnectionAsync(cancellationToken);
        for (var index = 0; index < dto.Chunks.Count; index++)
        {
            var chunk = dto.Chunks[index];
            var vectorRowId = await InsertVectorAsync(connection, chunk.Embedding, entity.ApiKeyId, entity.KnowledgeGroupId, cancellationToken);
            dbContext.KnowledgeChunks.Add(chunk.ToEntity(entity, index, vectorRowId));
        }

        entity.ExtractedText = dto.ExtractedText;
        entity.Status = KnowledgeDocumentStatus.Indexed;
        entity.ErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<List<KnowledgeChunkMatchDto>> SearchAsync(Guid apiKeyId, List<double> embedding, int topK, CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // api_key_id is a vec0 partition key, so the KNN search is pre-filtered to the caller's own vectors.
        command.CommandText = "SELECT rowid, distance FROM vectors.vec_chunks WHERE embedding MATCH $embedding AND k = $k AND api_key_id = $apiKeyId ORDER BY distance;";
        command.Parameters.AddWithValue("$embedding", JsonSerializer.Serialize(embedding));
        command.Parameters.AddWithValue("$k", topK);
        command.Parameters.AddWithValue("$apiKeyId", apiKeyId.ToString());

        var matches = new List<(long VectorRowId, double Distance)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add((reader.GetInt64(0), reader.GetDouble(1)));
            }
        }

        if (matches.Count == 0)
        {
            return [];
        }

        var vectorRowIds = matches.Select(match => match.VectorRowId).ToList();
        var chunks = await (
            from chunk in dbContext.KnowledgeChunks.AsNoTracking()
            join document in dbContext.KnowledgeDocuments.AsNoTracking() on chunk.KnowledgeDocumentId equals document.Id
            where chunk.ApiKeyId == apiKeyId && vectorRowIds.Contains(chunk.VectorRowId)
            select new { chunk.VectorRowId, chunk.Id, chunk.KnowledgeDocumentId, chunk.Text, document.OriginalFileName, document.KnowledgeGroupId }
        ).ToDictionaryAsync(x => x.VectorRowId, cancellationToken);

        return matches
            .Where(match => chunks.ContainsKey(match.VectorRowId))
            .Select(match =>
            {
                var chunk = chunks[match.VectorRowId];
                return KnowledgeChunkMapper.ToMatchDto(chunk.Id, chunk.KnowledgeDocumentId, chunk.OriginalFileName, chunk.KnowledgeGroupId, chunk.Text, match.Distance);
            })
            .ToList();
    }

    public async Task<bool> DeleteAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId && x.Id == id)
            .Select(x => new { x.RelativePath })
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return false;
        }

        var vectorRowIds = await dbContext.KnowledgeChunks.AsNoTracking()
            .Where(x => x.KnowledgeDocumentId == id)
            .Select(x => x.VectorRowId)
            .ToListAsync(cancellationToken);

        // The database cascades to the chunk rows.
        await dbContext.KnowledgeDocuments.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
        await DeleteVectorsAsync(vectorRowIds, cancellationToken);
        TryDeleteFile(document.RelativePath);
        return true;
    }

    private IQueryable<KnowledgeDocument> Filter(Guid apiKeyId, Guid? knowledgeGroupId)
    {
        var query = dbContext.KnowledgeDocuments.AsNoTracking().Where(x => x.ApiKeyId == apiKeyId);
        return knowledgeGroupId is { } groupId ? query.Where(x => x.KnowledgeGroupId == groupId) : query;
    }

    private static async Task<long> InsertVectorAsync(SqliteConnection connection, List<double> embedding, Guid apiKeyId, Guid groupId, CancellationToken cancellationToken)
    {
        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = "INSERT INTO vectors.vec_chunks(embedding, api_key_id, group_id) VALUES ($embedding, $apiKeyId, $groupId);";
            insertCommand.Parameters.AddWithValue("$embedding", JsonSerializer.Serialize(embedding));
            insertCommand.Parameters.AddWithValue("$apiKeyId", apiKeyId.ToString());
            insertCommand.Parameters.AddWithValue("$groupId", groupId.ToString());
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var rowIdCommand = connection.CreateCommand();
        rowIdCommand.CommandText = "SELECT last_insert_rowid();";
        return (long)(await rowIdCommand.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task DeleteVectorsAsync(List<long> vectorRowIds, CancellationToken cancellationToken)
    {
        if (vectorRowIds.Count == 0)
        {
            return;
        }

        var connection = await GetOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // The ids are our own stored rowids (longs), never user input, and vec0 has no array parameter for IN (...).
        command.CommandText = $"DELETE FROM vectors.vec_chunks WHERE rowid IN ({string.Join(",", vectorRowIds)});";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        return connection;
    }

    private string GetAbsolutePath(string relativePath) =>
        Path.Combine(environment.ContentRootPath, "app_files", relativePath);

    // Best-effort: the row is already gone and is the source of truth. Under OneDrive-synced folders the
    // sync driver can briefly lock a file, so a failed delete is logged instead of failing the request.
    private void TryDeleteFile(string relativePath)
    {
        var absolutePath = GetAbsolutePath(relativePath);
        try
        {
            File.Delete(absolutePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Failed to delete knowledge document file {FilePath}", absolutePath);
        }
    }
}
