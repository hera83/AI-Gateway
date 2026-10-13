using api.Repositories.KnowledgeDocuments.Interfaces;

namespace api.BgWorkers;

// Removes knowledge base files in app_files/ that no document row points to: files whose delete failed because
// something held them (OneDrive's sync driver, a download in progress). Runs at startup and then every hour.
public class KnowledgeFileCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<KnowledgeFileCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    // An upload writes its file just before its row, so younger files are left alone.
    private static readonly TimeSpan MinFileAge = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IKnowledgeDocumentRepository>();
                    var deleted = await repository.DeleteOrphanedFilesAsync(MinFileAge, stoppingToken);
                    if (deleted > 0)
                    {
                        logger.LogInformation("Deleted {Count} knowledge base files without a document", deleted);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected on shutdown.
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "{Worker} failed", nameof(KnowledgeFileCleanupWorker));
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // WaitForNextTickAsync throws when stoppingToken is cancelled.
        }
    }
}
