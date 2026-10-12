using System.Text;
using api.Managers.KnowledgeBase.Dtos;
using api.Services.Tesseract.Dtos.Recognize;

namespace api.Managers.KnowledgeBase;

// Turns Tesseract's words into text and decides whether it is good enough to index. Unsure words and lines
// (specks, edges, photos, graphics) are dropped first, then the rest must be confident and look like real words.
public static class OcrTextBuilder
{
    public static OcrTextResultDto Build(IReadOnlyList<WordDto> words, KnowledgeBaseOcrSettings settings)
    {
        var recognized = words.Where(word => word.Confidence >= 0 && !string.IsNullOrWhiteSpace(word.Text)).ToList();
        var lines = recognized
            .Where(word => word.Confidence >= settings.MinWordConfidence)
            .GroupBy(word => (word.BlockNumber, word.ParagraphNumber, word.LineNumber))
            .Where(line => WeightedConfidence(line) >= settings.MinLineConfidence
                && line.Any(word => word.Text.Count(char.IsLetterOrDigit) >= 2))
            .ToList();

        if (lines.Count == 0)
        {
            return new OcrTextResultDto
            {
                RecognizedWordCount = recognized.Count,
                RejectionReason = recognized.Count == 0 ? "no text recognized" : "no confidently recognized lines"
            };
        }

        // One line per OCR line, and a blank line between paragraphs.
        var text = new StringBuilder();
        (int Block, int Paragraph)? previousParagraph = null;
        foreach (var line in lines)
        {
            var paragraph = (line.Key.BlockNumber, line.Key.ParagraphNumber);
            if (previousParagraph is not null)
            {
                text.Append(Environment.NewLine);
                if (paragraph != previousParagraph)
                {
                    text.Append(Environment.NewLine);
                }
            }

            text.AppendJoin(' ', line.Select(word => word.Text));
            previousParagraph = paragraph;
        }

        var result = new OcrTextResultDto
        {
            RecognizedWordCount = recognized.Count,
            Text = text.ToString(),
            Confidence = WeightedConfidence(lines.SelectMany(line => line))
        };

        // Upside-down or garbled pages lose most of their words to the filters above, and the few left can
        // look confident by chance. Good pages keep nearly everything.
        var keptPercent = 100.0 * lines.Sum(line => line.Sum(word => word.Text.Length)) / recognized.Sum(word => word.Text.Length);

        var quality = TextQuality.Analyze(result.Text);
        if (keptPercent < settings.MinKeptPercent)
        {
            result.RejectionReason = $"only {keptPercent:0}% of the recognized text was confident (needs {settings.MinKeptPercent}%)";
        }
        else if (result.Confidence < settings.MinConfidence)
        {
            result.RejectionReason = $"mean confidence {result.Confidence:0} is below {settings.MinConfidence}";
        }
        else if (quality.LetterOrDigitCount < settings.MinCharacters)
        {
            result.RejectionReason = $"only {quality.LetterOrDigitCount} letters and digits";
        }
        else if (TextQuality.IsGarbled(quality))
        {
            result.RejectionReason = $"does not look like words ({quality.WordLikeRatio:P0} word-like, {quality.AlphanumericRatio:P0} letters and digits)";
        }

        return result;
    }

    // Weighted by length, so a few confident short words can't outweigh a garbled long one.
    private static double WeightedConfidence(IEnumerable<WordDto> words)
    {
        double weightedSum = 0, totalLength = 0;
        foreach (var word in words)
        {
            weightedSum += word.Confidence * word.Text.Length;
            totalLength += word.Text.Length;
        }

        return totalLength == 0 ? 0 : weightedSum / totalLength;
    }
}
