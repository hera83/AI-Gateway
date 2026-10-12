using System.Text.Json;

namespace api.Services.Speaches.Dtos.Errors;

// Speaches is FastAPI-based ({"detail": ...}), while its OpenAI-compatible routes may use {"error": {...}}.
public class ErrorResponseDto
{
    public JsonElement? Detail { get; set; }

    public JsonElement? Error { get; set; }
}
