using System.Net;

namespace api.Services.Speaches;

// Thrown by SpeachesService.EnsureSuccessAsync. GlobalExceptionHandler forwards StatusCode as-is.
public class SpeachesException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
