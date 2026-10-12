namespace api.Services.Speaches;

public class SpeachesSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8000";

    // Long transcriptions can take up to an hour, so the default HttpClient timeout (100 s) is far too short.
    public int TimeoutMinutes { get; set; } = 75;
}
