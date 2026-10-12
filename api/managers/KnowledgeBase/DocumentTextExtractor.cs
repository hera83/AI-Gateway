using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace api.Managers.KnowledgeBase;

// Only used by the knowledge base, so it lives with the feature instead of in extensions/.
public static class DocumentTextExtractor
{
    private static readonly string[] SupportedExtensions = [".txt", ".md", ".pdf", ".docx"];

    public static bool IsSupported(string fileName) =>
        SupportedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    public static async Task<string> ExtractTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".txt" or ".md" => await new StreamReader(fileStream).ReadToEndAsync(cancellationToken),
            ".pdf" => ExtractPdfText(fileStream),
            ".docx" => ExtractDocxText(fileStream),
            _ => throw new NotSupportedException($"File '{fileName}' has an unsupported file type.")
        };

    private static string ExtractPdfText(Stream fileStream)
    {
        using var document = PdfDocument.Open(fileStream);
        return string.Join(Environment.NewLine, document.GetPages().Select(page => page.Text));
    }

    private static string ExtractDocxText(Stream fileStream)
    {
        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body is null
            ? string.Empty
            : string.Join(Environment.NewLine, body.Elements<Paragraph>().Select(paragraph => paragraph.InnerText));
    }
}
