using api.Services.Ollama;
using api.Services.Speaches;
using Microsoft.AspNetCore.Diagnostics;

namespace api.Infrastructure.ErrorHandling;

public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, detail) = Classify(exception);

        logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Detail = detail }
        });
    }

    private (int Status, string? Detail) Classify(Exception exception) => exception switch
    {
        OllamaException ollamaException => ((int)ollamaException.StatusCode, ollamaException.Message),

        SpeachesException speachesException => ((int)speachesException.StatusCode, speachesException.Message),

        // E.g. a request body above Kestrel:Limits:MaxRequestBodySize.
        BadHttpRequestException badRequestException => (badRequestException.StatusCode, badRequestException.Message),

        // Connection-level failure reaching Ollama/Speaches (no HTTP response at all).
        HttpRequestException httpException => (StatusCodes.Status502BadGateway, httpException.Message),

        TaskCanceledException or OperationCanceledException => (StatusCodes.Status504GatewayTimeout, "The upstream request timed out."),

        _ => (StatusCodes.Status500InternalServerError, environment.IsDevelopment() ? exception.Message : null)
    };
}
