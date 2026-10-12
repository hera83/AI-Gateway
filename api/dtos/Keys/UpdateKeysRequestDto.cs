using System.ComponentModel.DataAnnotations;
using api.Extensions;

namespace api.Dtos.Keys;

public class UpdateKeysRequestDto : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ResponsibleName { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string ContactInfo { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }

    // Same reason as CreateKeysRequestDto: an update must not silently expire a key.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpiresAt.AsUtc() <= DateTime.UtcNow)
        {
            yield return new ValidationResult("ExpiresAt must be in the future.", [nameof(ExpiresAt)]);
        }
    }
}
