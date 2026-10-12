using System.Globalization;
using api.Managers.KnowledgeBase.Dtos;

namespace api.Managers.KnowledgeBase;

// Cheap plausibility checks that tell real text from OCR noise and from PDFs whose fonts have no usable
// Unicode mapping. Tuned for Danish and English, but language-neutral enough not to reject other text.
public static class TextQuality
{
    private const double MaxSuspiciousRatio = 0.05;
    private const double MinAlphanumericRatio = 0.5;
    private const double MinWordLikeRatio = 0.6;

    // Share of an OCR text's words that must already be in the page text for it to count as a duplicate.
    private const double DuplicateWordShare = 0.8;

    private const string Vowels = "aeiouyæøåäöüéèêëáàâíìîóòôúùû";

    public static TextQualityDto Analyze(string text)
    {
        int nonWhitespace = 0, alphanumeric = 0, suspicious = 0;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            nonWhitespace++;
            if (char.IsLetterOrDigit(character))
            {
                alphanumeric++;
            }
            else if (IsSuspicious(character))
            {
                suspicious++;
            }
        }

        int tokens = 0, wordLike = 0;
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var core = TrimToLettersAndDigits(token);
            if (core.IsEmpty)
            {
                continue;
            }

            tokens++;
            if (IsWordLike(core))
            {
                wordLike++;
            }
        }

        return new TextQualityDto
        {
            LetterOrDigitCount = alphanumeric,
            AlphanumericRatio = nonWhitespace == 0 ? 0 : (double)alphanumeric / nonWhitespace,
            SuspiciousRatio = nonWhitespace == 0 ? 0 : (double)suspicious / nonWhitespace,
            WordLikeRatio = tokens == 0 ? 0 : (double)wordLike / tokens
        };
    }

    public static bool IsGarbled(TextQualityDto quality) =>
        quality.SuspiciousRatio > MaxSuspiciousRatio
        || quality.AlphanumericRatio < MinAlphanumericRatio
        || quality.WordLikeRatio < MinWordLikeRatio;

    // True when most words in candidate already appear in reference, e.g. OCR of a figure whose labels are
    // also in the page text, or a scan whose text layer was already OCR'ed by the scanner.
    public static bool IsMostlyContainedIn(string candidate, string reference)
    {
        var candidateWords = NormalizedWords(candidate).ToList();
        if (candidateWords.Count == 0)
        {
            return false;
        }

        var referenceWords = NormalizedWords(reference).ToHashSet();
        return candidateWords.Count(referenceWords.Contains) >= candidateWords.Count * DuplicateWordShare;
    }

    private static bool IsSuspicious(char character) =>
        char.IsControl(character)
        || character == '�'
        || character is >= '' and <= ''
        || char.GetUnicodeCategory(character) == UnicodeCategory.OtherNotAssigned;

    // Words have a vowel ("og", "i", "data"), numbers have a digit ("2024", "3a"), acronyms are short and
    // all caps ("PDF", "A/S"). OCR noise tends to be none of these ("rn", "lIlI", "~=|", "wwww").
    private static bool IsWordLike(ReadOnlySpan<char> core)
    {
        if (core.Length > 40)
        {
            return false;
        }

        int letters = 0, digits = 0, vowels = 0, uppercase = 0, letterRun = 0;
        for (var index = 0; index < core.Length; index++)
        {
            var character = core[index];
            if (char.IsLetter(character))
            {
                letters++;
                if (char.IsUpper(character))
                {
                    uppercase++;
                }

                if (Vowels.Contains(char.ToLowerInvariant(character)))
                {
                    vowels++;
                }

                letterRun = index > 0 && char.ToLowerInvariant(core[index - 1]) == char.ToLowerInvariant(character) ? letterRun + 1 : 1;
                if (letterRun >= 4)
                {
                    return false;
                }
            }
            else if (char.IsDigit(character))
            {
                digits++;
            }
        }

        if (letters + digits < core.Length * 0.6)
        {
            return false;
        }

        return digits > 0 || vowels > 0 || (uppercase == letters && letters is >= 2 and <= 6);
    }

    private static ReadOnlySpan<char> TrimToLettersAndDigits(string token)
    {
        var start = 0;
        var end = token.Length - 1;
        while (start <= end && !char.IsLetterOrDigit(token[start]))
        {
            start++;
        }

        while (end >= start && !char.IsLetterOrDigit(token[end]))
        {
            end--;
        }

        return token.AsSpan(start, end - start + 1);
    }

    private static IEnumerable<string> NormalizedWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => new string(token.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant())
            .Where(word => word.Length >= 3);
}
