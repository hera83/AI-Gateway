using System.ComponentModel.DataAnnotations;

namespace api.Dtos.KnowledgeBase;

public class CreateGroupKnowledgeBaseRequestDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
}
