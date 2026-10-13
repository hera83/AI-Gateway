using api.Managers.KnowledgeBase;
using api.Managers.KnowledgeBase.Interfaces;
using Microsoft.Extensions.Options;

namespace api.BgWorkers;

// Extracts (incl. OCR), chunks, embeds and indexes uploaded knowledge documents, one at a time and oldest first.
// The documents table is the queue (Status "Processing"), so uploads left waiting by a restart are picked up on
// the next start. Waits on KnowledgeDocumentProcessingSignal instead of a PeriodicTimer, so a new upload is
// started at once; WorkerIntervalSeconds is only the fallback check.
public class KnowledgeDocumentProcessingWorker(
    IServiceScopeFactory scopeFactory,
    KnowledgeDocumentProcessingSignal signal,
    IOptions<KnowledgeBaseSettings> options,
    ILogger<KnowledgeDocumentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.WorkerIntervalSeconds));
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    while (await ProcessNextDocumentAsync(stoppingToken))
                    {
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected on shutdown. The document stays "Processing" and is picked up on the next start.
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "{Worker} failed", nameof(KnowledgeDocumentProcessingWorker));
                }

                await signal.WaitAsync(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // WaitAsync throws when stoppingToken is cancelled.
        }
    }

    // A new scope (and DbContext) per document, so one document's tracked entities never affect the next.
    private async Task<bool> ProcessNextDocumentAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IKnowledgeBaseManager>();
        return await manager.ProcessNextDocumentAsync(stoppingToken);
    }
}
