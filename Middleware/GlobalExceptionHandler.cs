using AiGateway.Dto.Errors;
using AiGateway.Service.KnowledgeBase;
using AiGateway.Service.Keys;
using AiGateway.Service.TextExtraction;
using Microsoft.AspNetCore.Diagnostics;

namespace AiGateway.Middleware;

public class GlobalExceptionHandler(IHostEnvironment environment, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = Classify(exception);

        logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        var response = new ErrorResponseDto
        {
            Status = status,
            Title = title,
            Detail = detail,
            TraceId = httpContext.TraceIdentifier
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    private (int Status, string Title, string? Detail) Classify(Exception exception) => exception switch
    {
        ApiKeyNotFoundException notFoundEx =>
            (StatusCodes.Status404NotFound, "API key not found", notFoundEx.Message),

        KnowledgeGroupNotFoundException notFoundEx =>
            (StatusCodes.Status404NotFound, "Knowledge group not found", notFoundEx.Message),

        KnowledgeDocumentNotFoundException notFoundEx =>
            (StatusCodes.Status404NotFound, "Knowledge document not found", notFoundEx.Message),

        DocumentFileNotFoundException fileNotFoundEx =>
            (StatusCodes.Status404NotFound, "Knowledge document file not found", fileNotFoundEx.Message),

        KnowledgeGroupNameConflictException conflictEx =>
            (StatusCodes.Status409Conflict, "Knowledge group name conflict", conflictEx.Message),

        UploadTooLargeException tooLargeEx =>
            (StatusCodes.Status413PayloadTooLarge, "Upload too large", tooLargeEx.Message),

        UnsupportedFileTypeException unsupportedEx =>
            (StatusCodes.Status400BadRequest, "Unsupported file type", unsupportedEx.Message),

        MasterKeyNotSupportedException masterKeyEx =>
            (StatusCodes.Status403Forbidden, "Master key not supported", masterKeyEx.Message),

        NoUserMessageException noUserMessageEx =>
            (StatusCodes.Status400BadRequest, "No user message found", noUserMessageEx.Message),

        // The HttpClient wrappers in Service/Ollama and Service/Speaches throw this with the
        // upstream's own status code attached, so surface that code (and its message) as-is.
        HttpRequestException { StatusCode: not null } httpEx =>
            ((int)httpEx.StatusCode, "Upstream request failed", httpEx.Message),

        HttpRequestException httpEx =>
            (StatusCodes.Status502BadGateway, "Upstream service unreachable", httpEx.Message),

        TaskCanceledException or OperationCanceledException =>
            (StatusCodes.Status504GatewayTimeout, "Upstream request timed out", null),

        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", environment.IsDevelopment() ? exception.Message : null)
    };
}
