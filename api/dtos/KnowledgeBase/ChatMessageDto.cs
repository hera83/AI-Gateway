using System.ComponentModel.DataAnnotations;

namespace api.Dtos.KnowledgeBase;

public class ChatMessageDto
{
    [Required]
    public string Role { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}
