using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace api.Services.Speaches;

public partial class SpeachesService
{
    public async Task ProxyRealtimeTranscriptionAsync(WebSocket clientSocket, string model, string? language, CancellationToken cancellationToken = default)
    {
        // Speaches' realtime endpoint is the same host/port as the HTTP API, just upgraded to a WebSocket.
        var httpClient = await httpClientFactory.CreateClientAsync(cancellationToken);
        var baseAddress = httpClient.BaseAddress!;
        var query = $"intent=transcription&model={Uri.EscapeDataString(model)}";
        if (language is not null)
        {
            query += $"&language={Uri.EscapeDataString(language)}";
        }

        var upstreamUri = new UriBuilder(baseAddress)
        {
            Scheme = baseAddress.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
            Path = "/v1/realtime",
            Query = query
        }.Uri;

        using var upstreamSocket = new ClientWebSocket();
        await upstreamSocket.ConnectAsync(upstreamUri, cancellationToken);

        // Each direction is driven only by the request's own cancellation token, never by the other
        // direction failing. Cancelling a pending WebSocket.ReceiveAsync aborts the whole socket, so a
        // shared "stop the other side" token would tear the client connection down before
        // TryNotifyClientOfFailureAsync could run. Don't reintroduce that.
        var clientToUpstream = RelayAsync(clientSocket, upstreamSocket, cancellationToken);
        var upstreamToClient = RelayAsync(upstreamSocket, clientSocket, cancellationToken);

        var first = await Task.WhenAny(clientToUpstream, upstreamToClient);
        if (first.IsFaulted)
        {
            var exception = first.Exception!.GetBaseException();
            logger.LogError(exception, "Speaches realtime transcription relay failed for model '{Model}'.", model);
            await TryNotifyClientOfFailureAsync(clientSocket, exception);
        }

        // Give the other direction a moment to finish (e.g. acking the close frame) without blocking forever.
        // Its own exception is only observed here; the failure that mattered was handled above.
        var second = first == clientToUpstream ? upstreamToClient : clientToUpstream;
        try
        {
            await second.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        catch
        {
            // Timeout or fault on the trailing direction: nothing more to do.
        }
    }

    // Best-effort: sends an OpenAI Realtime-style "error" event so clients learn why the connection ends.
    // CloseOutputAsync (not CloseAsync) and CancellationToken.None are deliberate: the other direction may
    // still have a receive pending on this socket, and the request token may already have tripped.
    private static async Task TryNotifyClientOfFailureAsync(WebSocket clientSocket, Exception exception)
    {
        if (clientSocket.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            var errorEvent = JsonSerializer.Serialize(new
            {
                type = "error",
                error = new
                {
                    type = "server_error",
                    message = $"The realtime transcription connection to Speaches failed: {exception.Message}"
                }
            });
            await clientSocket.SendAsync(Encoding.UTF8.GetBytes(errorEvent), WebSocketMessageType.Text, true, CancellationToken.None);
            await clientSocket.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, "Upstream relay failed.", CancellationToken.None);
        }
        catch
        {
            // The client socket may already be unusable.
        }
    }

    // The realtime protocol is JSON text-frame events (audio is base64 inside them), so whole messages are
    // shuttled verbatim in both directions until one side closes.
    private static async Task RelayAsync(WebSocket source, WebSocket destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (source.State == WebSocketState.Open)
        {
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await source.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (destination.State == WebSocketState.Open)
                    {
                        await destination.CloseOutputAsync(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, cancellationToken);
                    }

                    return;
                }

                messageStream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (destination.State == WebSocketState.Open)
            {
                await destination.SendAsync(messageStream.ToArray(), result.MessageType, true, cancellationToken);
            }
        }
    }
}
