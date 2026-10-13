using System.Threading.Channels;

namespace api.Managers.KnowledgeBase;

// Singleton that wakes KnowledgeDocumentProcessingWorker when an upload is stored, so it doesn't have to poll
// the database often. The database is still the queue: a missed signal only delays a document until the
// worker's next fallback check.
public class KnowledgeDocumentProcessingSignal
{
    // Holds at most one pending signal: any number of uploads while the worker is busy cause one extra check.
    private readonly Channel<bool> channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public void Notify() => channel.Writer.TryWrite(true);

    // Returns when signalled or when the timeout runs out, whichever comes first.
    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await channel.Reader.ReadAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The timeout ran out.
        }
    }
}
