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

    // Not scoped to a key: used by the background worker, which processes every key's uploads, oldest first.
    public Task<KnowledgeDocumentDto?> GetNextProcessingAsync(CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.Status == KnowledgeDocumentStatus.Processing)
            .OrderBy(x => x.CreatedAt)
            .Select(KnowledgeDocumentMapper.ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

    // Indexed documents whose Markdown is missing or made by an older converter version. Not scoped to a key: the
    // background worker makes it again.
    public Task<KnowledgeDocumentDto?> GetNextWithOutdatedMarkdownAsync(int currentVersion, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.Status == KnowledgeDocumentStatus.Indexed && (x.MarkdownText == null || x.MarkdownVersion < currentVersion))
            .OrderBy(x => x.CreatedAt)
            .Select(KnowledgeDocumentMapper.ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<KnowledgeDocumentMarkdownDto?> GetMarkdownAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId && x.Id == id)
            .Select(KnowledgeDocumentMapper.ToMarkdownDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

    // Not scoped to a key: the LLM worker lays out every key's documents, oldest first. Only Markdown of the current
    // version, so a document that is about to be made again is never laid out from its old Markdown.
    public Task<KnowledgeDocumentLlmMarkdownDto?> GetNextMarkdownLlmPendingAsync(int currentVersion, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.Status == KnowledgeDocumentStatus.Indexed &&
                        x.MarkdownSource == KnowledgeMarkdownSource.LlmPending &&
                        x.MarkdownVersion == currentVersion &&
                        x.MarkdownText != null)
            .OrderBy(x => x.CreatedAt)
            .Select(KnowledgeDocumentMapper.ToLlmMarkdownDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> SetMarkdownAsync(Guid id, string markdown, bool needsLlm, int version, CancellationToken cancellationToken = default) =>
        UpdateMarkdownAsync(id, entity =>
        {
            entity.MarkdownText = markdown;
            entity.MarkdownSource = needsLlm ? KnowledgeMarkdownSource.LlmPending : KnowledgeMarkdownSource.Structure;
            entity.MarkdownVersion = version;
        }, cancellationToken);

    // Saved after every part, so the LLM layout continues from here after a restart.
    public Task<bool> SaveLlmMarkdownProgressAsync(Guid id, string draft, int partsDone, int partsTotal, CancellationToken cancellationToken = default) =>
        UpdateMarkdownAsync(id, entity =>
        {
            entity.MarkdownLlmDraft = draft;
            entity.MarkdownLlmPartsDone = partsDone;
            entity.MarkdownLlmPartsTotal = partsTotal;
        }, cancellationToken, keepLlmProgress: true);

    public Task<bool> SetLlmMarkdownAsync(Guid id, string markdown, CancellationToken cancellationToken = default) =>
        UpdateMarkdownAsync(id, entity =>
        {
            entity.MarkdownText = markdown;
            entity.MarkdownSource = KnowledgeMarkdownSource.Llm;
        }, cancellationToken);

    // Keeps the Markdown made from the structure and takes the document out of the LLM queue.
    public Task<bool> SkipLlmMarkdownAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateMarkdownAsync(id, entity => entity.MarkdownSource = KnowledgeMarkdownSource.Structure, cancellationToken);

    public Task<KnowledgeDocumentTextDto?> GetTextAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId && x.Id == id)
            .Select(KnowledgeDocumentMapper.ToTextDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

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

    // FileShare.Delete: the background worker reads a file for minutes (OCR), and the document may be deleted
    // meanwhile. Without it Windows refuses the delete and the file is left behind; with it the file goes as soon as
    // the reader closes it.
    public async Task<Stream?> OpenFileAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default)
    {
        var file = await GetFileAsync(apiKeyId, id, cancellationToken);
        return file is null
            ? null
            : new FileStream(file.AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, bufferSize: 81920, useAsync: true);
    }

    // Files without a row: left behind when a delete couldn't remove the file (OneDrive or a download held it).
    // Only files older than minAge, because CreateAsync writes the file just before its row.
    public async Task<int> DeleteOrphanedFilesAsync(TimeSpan minAge, CancellationToken cancellationToken = default)
    {
        var folder = GetAbsolutePath(FilesFolder);
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        var knownFiles = (await dbContext.KnowledgeDocuments.AsNoTracking().Select(x => x.RelativePath).ToListAsync(cancellationToken))
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cutoff = DateTime.UtcNow - minAge;
        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(path);
            if (fileName.StartsWith('.') || knownFiles.Contains(fileName) || File.GetLastWriteTimeUtc(path) > cutoff)
            {
                continue;
            }

            if (TryDeleteFile($"{FilesFolder}/{fileName}"))
            {
                deleted++;
            }
        }

        return deleted;
    }

    public async Task<KnowledgeDocumentDto?> MarkFailedAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default)
    {
        // A failed MarkIndexedAsync in the same scope leaves its chunks tracked, and saving them again would fail too.
        dbContext.ChangeTracker.Clear();
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

    // Indexes an Indexed document again (new chunks from its current Markdown). The new chunks and vectors are added
    // and the old ones removed in the same save, so a search never finds the document without chunks.
    // false = the document doesn't exist.
    public async Task<bool> ReplaceChunksAsync(Guid id, List<CreateKnowledgeChunkDto> chunks, int indexVersion, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var oldChunks = await dbContext.KnowledgeChunks.Where(x => x.KnowledgeDocumentId == id).ToListAsync(cancellationToken);
        var connection = await GetOpenConnectionAsync(cancellationToken);
        for (var index = 0; index < chunks.Count; index++)
        {
            var vectorRowId = await InsertVectorAsync(connection, chunks[index].Embedding, entity.ApiKeyId, entity.KnowledgeGroupId, cancellationToken);
            dbContext.KnowledgeChunks.Add(chunks[index].ToEntity(entity, index, vectorRowId));
        }

        dbContext.KnowledgeChunks.RemoveRange(oldChunks);
        entity.IndexVersion = indexVersion;
        await dbContext.SaveChangesAsync(cancellationToken);
        await DeleteVectorsAsync(oldChunks.Select(chunk => chunk.VectorRowId).ToList(), cancellationToken);
        return true;
    }

    // Indexed documents whose chunks were made by an older chunker version. Not scoped to a key: the background
    // worker indexes them again. Not while an LLM is laying out their Markdown; that indexes them when it finishes.
    public Task<KnowledgeDocumentDto?> GetNextWithOutdatedIndexAsync(int currentVersion, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.Status == KnowledgeDocumentStatus.Indexed &&
                        x.IndexVersion < currentVersion &&
                        x.MarkdownText != null &&
                        x.MarkdownSource != KnowledgeMarkdownSource.LlmPending)
            .OrderBy(x => x.CreatedAt)
            .Select(KnowledgeDocumentMapper.ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

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
        entity.MarkdownText = dto.Markdown;
        entity.MarkdownSource = dto.MarkdownNeedsLlm ? KnowledgeMarkdownSource.LlmPending : KnowledgeMarkdownSource.Structure;
        entity.MarkdownVersion = dto.MarkdownVersion;
        entity.IndexVersion = dto.IndexVersion;
        ClearLlmProgress(entity);
        entity.Status = KnowledgeDocumentStatus.Indexed;
        entity.ErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<List<KnowledgeChunkMatchDto>> SearchAsync(
        Guid apiKeyId, List<double> embedding, int topK, Guid? knowledgeGroupId, CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // api_key_id is a vec0 partition key, so the KNN search is pre-filtered to the caller's own vectors.
        // group_id is a metadata column, so it filters inside the KNN search too: k matches from that group alone.
        command.CommandText = knowledgeGroupId is null
            ? "SELECT rowid, distance FROM vectors.vec_chunks WHERE embedding MATCH $embedding AND k = $k AND api_key_id = $apiKeyId ORDER BY distance;"
            : "SELECT rowid, distance FROM vectors.vec_chunks WHERE embedding MATCH $embedding AND k = $k AND api_key_id = $apiKeyId AND group_id = $groupId ORDER BY distance;";
        command.Parameters.AddWithValue("$embedding", JsonSerializer.Serialize(embedding));
        command.Parameters.AddWithValue("$k", topK);
        command.Parameters.AddWithValue("$apiKeyId", apiKeyId.ToString());
        if (knowledgeGroupId is { } groupId)
        {
            command.Parameters.AddWithValue("$groupId", groupId.ToString());
        }

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

    // Any other change to the Markdown ends an LLM layout in progress, so its draft is cleared.
    private async Task<bool> UpdateMarkdownAsync(Guid id, Action<KnowledgeDocument> update, CancellationToken cancellationToken, bool keepLlmProgress = false)
    {
        var entity = await dbContext.KnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        if (!keepLlmProgress)
        {
            ClearLlmProgress(entity);
        }

        update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ClearLlmProgress(KnowledgeDocument entity)
    {
        entity.MarkdownLlmDraft = null;
        entity.MarkdownLlmPartsDone = null;
        entity.MarkdownLlmPartsTotal = null;
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
    // sync driver can briefly lock a file, so a failed delete is logged instead of failing the request;
    // KnowledgeFileCleanupWorker removes the file later (DeleteOrphanedFilesAsync).
    private bool TryDeleteFile(string relativePath)
    {
        var absolutePath = GetAbsolutePath(relativePath);
        try
        {
            File.Delete(absolutePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Failed to delete knowledge document file {FilePath}", absolutePath);
            return false;
        }
    }
}
