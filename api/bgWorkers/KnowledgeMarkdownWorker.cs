using api.Managers.KnowledgeBase;
using api.Managers.KnowledgeBase.Interfaces;
using Microsoft.Extensions.Options;

namespace api.BgWorkers;

// Lays out the Markdown of mostly-OCR knowledge documents with an LLM (Ollama), one at a time. Separate from
// KnowledgeDocumentProcessingWorker, because a long scan can take minutes per document and must not hold up
// new uploads. Not urgent, so it checks every WorkerIntervalSeconds instead of being woken.
public class KnowledgeMarkdownWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<KnowledgeBaseSettings> options,
    ILogger<KnowledgeMarkdownWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.WorkerIntervalSeconds)));
        try
        {
            do
            {
                try
                {
                    while (await ImproveNextMarkdownAsync(stoppingToken))
                    {
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected on shutdown. The document stays pending and is laid out after the next start.
                }
                catch (Exception exception)
                {
                    // Typically Ollama can't be reached; the document stays pending and is tried again next time.
                    logger.LogError(exception, "{Worker} failed", nameof(KnowledgeMarkdownWorker));
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // WaitForNextTickAsync throws when stoppingToken is cancelled.
        }
    }

    // A new scope (and DbContext) per document.
    private async Task<bool> ImproveNextMarkdownAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IKnowledgeBaseManager>();
        return await manager.ImproveNextMarkdownAsync(stoppingToken);
    }
}
