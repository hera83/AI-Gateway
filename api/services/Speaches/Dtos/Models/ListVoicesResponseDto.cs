namespace api.Services.Speaches.Dtos.Models;

// GET v1/audio/voices answers { "voices": [...] }, not the { "data": [...] } that Speaches' own OpenAPI spec claims.
public class ListVoicesResponseDto
{
    public List<VoiceDto> Voices { get; set; } = [];

    public string Object { get; set; } = "list";
}
