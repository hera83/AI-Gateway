using System.Net;

namespace api.Services.Ollama;

// Thrown by OllamaService.EnsureSuccessAsync. GlobalExceptionHandler forwards StatusCode as-is.
public class OllamaException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
