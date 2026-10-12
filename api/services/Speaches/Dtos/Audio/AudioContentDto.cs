namespace api.Services.Speaches.Dtos.Audio;

public class AudioContentDto
{
    public byte[] Content { get; set; } = [];

    public string ContentType { get; set; } = "application/octet-stream";
}
