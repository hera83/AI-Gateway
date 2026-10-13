using System.ComponentModel.DataAnnotations;

namespace api.Dtos.KnowledgeBase;

public class SearchKnowledgeBaseRequestDto
{
    [Required]
    public string Query { get; set; } = string.Empty;

    [Range(1, 100)]
    public int? TopK { get; set; }

    // Optional: only search documents in this group. Without it, every group the key owns is searched.
    public Guid? GroupId { get; set; }
}
