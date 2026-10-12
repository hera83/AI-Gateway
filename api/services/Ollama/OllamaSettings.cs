namespace api.Services.Ollama;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";

    // The default HttpClient timeout (100 s) is too short for chat/embed on slow models or cold loads.
    public int TimeoutMinutes { get; set; } = 15;
}
