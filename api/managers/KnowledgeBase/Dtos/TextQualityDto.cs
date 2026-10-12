namespace api.Managers.KnowledgeBase.Dtos;

// Measured by TextQuality.Analyze. All ratios are 0-1.
public class TextQualityDto
{
    public int LetterOrDigitCount { get; set; }

    // Letters and digits out of all non-whitespace characters. Low for symbol soup like "|~=—".
    public double AlphanumericRatio { get; set; }

    // Control, replacement (U+FFFD) and private-use characters out of all non-whitespace characters.
    // High when a PDF font has no usable Unicode mapping.
    public double SuspiciousRatio { get; set; }

    // Tokens that look like words, numbers or acronyms out of all tokens with letters or digits.
    public double WordLikeRatio { get; set; }
}
