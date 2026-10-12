using System.ComponentModel.DataAnnotations;

namespace api.Dtos.KnowledgeBase;

public class UploadDocumentKnowledgeBaseRequestDto
{
    [Required]
    public Guid GroupId { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}
