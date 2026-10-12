using System.ComponentModel.DataAnnotations;
using api.Extensions;

namespace api.Dtos.Keys;

public class CreateKeysRequestDto : IValidatableObject
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

    // Without this, a key could be created already expired (e.g. Swagger UI's "now" example value) and
    // would fail on first use with no obvious reason.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpiresAt.AsUtc() <= DateTime.UtcNow)
        {
            yield return new ValidationResult("ExpiresAt must be in the future.", [nameof(ExpiresAt)]);
        }
    }
}
