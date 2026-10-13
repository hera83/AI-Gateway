using System.Text.RegularExpressions;
using api.Managers.KnowledgeBase.Dtos;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace api.Managers.KnowledgeBase;

// PDF → Markdown. A PDF has no structure, only positioned letters, so it is worked out from the layout:
// blocks in reading order (PdfPig's Docstrum + reading order detector), headings from font size and bold, and
// lists from bullets and numbers. Page numbers and headers/footers repeated on many pages are left out.
// OCR'ed text (scanned pages, images) comes in as paragraphs, labels and fields, without headers/footers repeated
// on many pages. Tables become paragraphs.
public static partial class PdfMarkdownConverter
{
    // A block is a heading candidate when its letters are this much larger than the body text.
    private const double HeadingSizeRatio = 1.15;
    private const int MaxHeadingLines = 3;
    private const int MaxHeadingCharacters = 200;

    // Distinct heading sizes become #, ## and ###. A short bold line at body size gets the level below.
    private const int MaxHeadingSizeLevels = 3;
    private const int MaxBoldHeadingCharacters = 120;

    private const double BoldLetterShare = 0.8;
    private const double MarginShare = 0.08;

    // Text in the margin found on at least this share of pages (and at least this many) is a header or footer.
    private const double RepeatedMarginPageShare = 0.5;
    private const int MinRepeatedMarginPages = 3;

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();

    // The page's text layer in blocks, in reading order. crop is the visible page in PdfPig's page space.
    public static List<PdfTextBlockDto> ReadBlocks(Page page, (double Left, double Bottom, double Right, double Top) crop)
    {
        if (page.Letters.Count == 0)
        {
            return [];
        }

        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
        var blocks = UnsupervisedReadingOrderDetector.Instance.Get(DocstrumBoundingBoxes.Instance.GetBlocks(words).ToList());
        var marginHeight = (crop.Top - crop.Bottom) * MarginShare;

        return blocks
            .Select(block =>
            {
                var letters = block.TextLines.SelectMany(line => line.Words).SelectMany(word => word.Letters)
                    .Where(letter => !string.IsNullOrWhiteSpace(letter.Value))
                    .ToList();
                return new PdfTextBlockDto
                {
                    Lines = block.TextLines.Select(line => line.Text.Trim()).Where(line => line.Length > 0).ToList(),
                    FontSize = Median(letters.Select(letter => letter.PointSize)),
                    IsBold = letters.Count > 0 && letters.Count(IsBold) >= letters.Count * BoldLetterShare,
                    IsInMargin = block.BoundingBox.Bottom >= crop.Top - marginHeight || block.BoundingBox.Top <= crop.Bottom + marginHeight
                };
            })
            .Where(block => block.Lines.Count > 0)
            .ToList();
    }

    // pageTexts says per page whether its text layer was used and which OCR texts were, in the same order as the
    // plain text, so the Markdown never holds more or less than the indexed text.
    public static string Render(IReadOnlyList<PdfPageDto> pages, IReadOnlyList<PdfPageTextDto> pageTexts)
    {
        var layoutBlocks = pages
            .Where((page, index) => pageTexts[index].UsesTextLayer)
            .SelectMany(page => page.Blocks ?? [])
            .ToList();
        var bodySize = BodyFontSize(layoutBlocks);
        var headingSizes = layoutBlocks
            .Where(block => IsLargeHeading(block, bodySize))
            .Select(block => RoundSize(block.FontSize))
            .Distinct()
            .OrderDescending()
            .Take(MaxHeadingSizeLevels)
            .ToList();
        var boldMarksHeadings = layoutBlocks.Sum(block => block.IsBold ? CharacterCount(block) : 0) < layoutBlocks.Sum(CharacterCount) / 2;
        var repeatedMarginTexts = FindRepeatedMarginTexts(pages, pageTexts);
        var ocrTexts = MarkdownFormatter.RemoveRepeatedPageLines(pageTexts.Select(page => string.Join("\n\n", page.OcrTexts)).ToList());

        var blocks = new List<(string Markdown, bool IsListItem)>();
        for (var index = 0; index < pages.Count; index++)
        {
            if (pageTexts[index].UsesTextLayer)
            {
                if (pages[index].Blocks is { } pageBlocks)
                {
                    foreach (var block in pageBlocks)
                    {
                        var text = string.Join(' ', block.Lines);
                        if (block.IsInMargin && (MarkdownFormatter.IsPageNumber(text) || repeatedMarginTexts.Contains(NormalizeMarginText(text))))
                        {
                            continue;
                        }

                        if (GetHeadingLevel(block, text, bodySize, headingSizes, boldMarksHeadings) is { } level)
                        {
                            blocks.Add((MarkdownFormatter.Heading(level, text), false));
                        }
                        else
                        {
                            MarkdownFormatter.AppendLines(blocks, block.Lines);
                        }
                    }
                }
                else
                {
                    MarkdownFormatter.AppendPlainText(blocks, pages[index].Text);
                }
            }

            MarkdownFormatter.AppendPlainText(blocks, ocrTexts[index]);
        }

        return MarkdownFormatter.Join(blocks);
    }

    private static int? GetHeadingLevel(PdfTextBlockDto block, string text, double bodySize, List<double> headingSizes, bool boldMarksHeadings)
    {
        if (IsLargeHeading(block, bodySize))
        {
            var sizeLevel = headingSizes.IndexOf(RoundSize(block.FontSize));
            return sizeLevel >= 0 ? sizeLevel + 1 : MaxHeadingSizeLevels;
        }

        // A single short bold line that doesn't end like a sentence ("Formål", "3. Økonomi").
        // A bold bullet is a list item, not a heading.
        if (boldMarksHeadings && block.IsBold && block.Lines.Count == 1 && text.Length <= MaxBoldHeadingCharacters &&
            text.Any(char.IsLetter) && !text.EndsWith('.') && !text.EndsWith(':') && !text.EndsWith(',') &&
            !(MarkdownFormatter.IsListLine(text) && !char.IsDigit(text[0])))
        {
            return Math.Min(headingSizes.Count + 1, MaxHeadingSizeLevels + 1);
        }

        return null;
    }

    private static bool IsLargeHeading(PdfTextBlockDto block, double bodySize) =>
        bodySize > 0 &&
        RoundSize(block.FontSize) >= bodySize * HeadingSizeRatio &&
        block.Lines.Count <= MaxHeadingLines &&
        CharacterCount(block) <= MaxHeadingCharacters &&
        block.Lines.Any(line => line.Any(char.IsLetter));

    // The size most of the text is set in, weighted by characters.
    private static double BodyFontSize(List<PdfTextBlockDto> blocks) =>
        blocks
            .GroupBy(block => RoundSize(block.FontSize))
            .Select(group => (Size: group.Key, Characters: group.Sum(CharacterCount)))
            .OrderByDescending(group => group.Characters)
            .Select(group => group.Size)
            .FirstOrDefault();

    private static HashSet<string> FindRepeatedMarginTexts(IReadOnlyList<PdfPageDto> pages, IReadOnlyList<PdfPageTextDto> pageTexts)
    {
        var pagesWithLayout = pages.Where((page, index) => pageTexts[index].UsesTextLayer && page.Blocks is not null).ToList();
        if (pagesWithLayout.Count < MinRepeatedMarginPages)
        {
            return [];
        }

        return pagesWithLayout
            .SelectMany(page => page.Blocks!.Where(block => block.IsInMargin).Select(block => NormalizeMarginText(string.Join(' ', block.Lines))).Distinct())
            .GroupBy(text => text)
            .Where(group => group.Count() >= Math.Max(MinRepeatedMarginPages, pagesWithLayout.Count * RepeatedMarginPageShare))
            .Select(group => group.Key)
            .ToHashSet();
    }

    // "Side 3 af 12" and "Side 4 af 12" are the same footer.
    private static string NormalizeMarginText(string text) =>
        DigitsRegex().Replace(MarkdownFormatter.CollapseWhitespace(text).ToLowerInvariant(), "#");

    private static bool IsBold(Letter letter)
    {
        if (letter.FontDetails?.IsBold == true)
        {
            return true;
        }

        var fontName = letter.FontName ?? string.Empty;
        return fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
               fontName.Contains("Black", StringComparison.OrdinalIgnoreCase) ||
               fontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase);
    }

    private static int CharacterCount(PdfTextBlockDto block) => block.Lines.Sum(line => line.Length);

    // Half points, so 11.98 and 12.02 count as the same size.
    private static double RoundSize(double size) => Math.Round(size * 2, MidpointRounding.AwayFromZero) / 2;

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }
}
