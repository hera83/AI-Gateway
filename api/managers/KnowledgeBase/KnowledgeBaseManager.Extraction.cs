using System.Drawing;
using api.Managers.KnowledgeBase.Dtos;
using api.Services.Tesseract.Dtos.Recognize;

namespace api.Managers.KnowledgeBase;

// Text extraction for uploads. Text files and Word documents are read directly (DocumentTextExtractor), and image
// files are OCR'ed page by page. A PDF page keeps its own text layer when it is real text, and Tesseract OCR
// fills in what the layer lacks: scanned pages, pages whose fonts give garbage, and text inside images.
// Every OCR result is quality-checked first (see OcrTextBuilder), so unreadable output never gets indexed.
public partial class KnowledgeBaseManager
{
    // Smaller images (in points, 72 per inch) are icons, bullets and logos, not text worth OCR'ing.
    private const float MinOcrImageWidth = 72;
    private const float MinOcrImageHeight = 18;

    // A near-full-page image on a page with plenty of real text is a background, or a scan whose text layer
    // the scanner already OCR'ed. OCR'ing it again would only repeat the text.
    private const double FullPageImageShare = 0.8;
    private const int PlentyOfTextCharacters = 100;

    // Tesseract's fully automatic page segmentation. Copes with pages turned 90°, but not upside down.
    private const int AutomaticPageSegmentation = 3;

    // Same, plus orientation detection (needs osd.traineddata). Slower, so only a retry for rejected whole pages.
    private const int AutomaticPageSegmentationWithOrientation = 1;

    // Plain text for chunking and Markdown for reading, from the same pass, so the two always hold the same content.
    private async Task<ExtractedDocumentDto> ExtractDocumentAsync(Stream fileStream, string fileName, Guid documentId, CancellationToken cancellationToken)
    {
        var kind = await DocumentTextExtractor.DetectKindAsync(fileStream, fileName, cancellationToken);
        switch (kind)
        {
            case DocumentKind.Pdf:
                return await ExtractPdfAsync(fileStream, documentId, cancellationToken);
            case DocumentKind.Image:
                return await ExtractImageAsync(fileStream, documentId, cancellationToken);
            case DocumentKind.Unsupported:
                throw new InvalidOperationException("The file type is not supported.");
        }

        var text = await DocumentTextExtractor.ExtractTextAsync(fileStream, kind, cancellationToken);
        var markdown = BuildMarkdown(() =>
        {
            if (kind != DocumentKind.Docx)
            {
                return TextMarkdownConverter.Convert(text, fileName);
            }

            fileStream.Position = 0;
            return DocxMarkdownConverter.Convert(fileStream);
        }, text, documentId);

        return new ExtractedDocumentDto { Text = text, Markdown = markdown, OcrShare = 0 };
    }

    // The Markdown is a nicer view of the same text, so a converter bug may never fail the upload: the plain
    // text is used as Markdown instead.
    private string BuildMarkdown(Func<string> convert, string text, Guid documentId)
    {
        try
        {
            return convert();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Markdown for knowledge document {DocumentId} fell back to plain text", documentId);
            return MarkdownFormatter.FromPlainText(text);
        }
    }

    // Every page of the image (a TIFF can have several) is quality-checked on its own, like a scanned PDF page.
    // No DPI is passed: photos and screenshots have none that fits, so Tesseract uses the image's own or estimates it.
    private async Task<ExtractedDocumentDto> ExtractImageAsync(Stream fileStream, Guid documentId, CancellationToken cancellationToken)
    {
        var ocrSettings = options.Value.Ocr;
        if (!ocrSettings.Enabled)
        {
            throw new InvalidOperationException("Text can't be read from images, because OCR is turned off (KnowledgeBase:Ocr:Enabled).");
        }

        byte[] image;
        using (var memory = new MemoryStream())
        {
            await fileStream.CopyToAsync(memory, cancellationToken);
            image = memory.ToArray();
        }

        var pages = await RecognizeImagePagesAsync(image, AutomaticPageSegmentation, ocrSettings, cancellationToken);

        // Upside-down scans read as garbage and get rejected; orientation detection turns them the right way round.
        if (pages.Values.Any(page => !page.IsAccepted && page.RecognizedWordCount > 0))
        {
            var retry = await RecognizeImagePagesAsync(image, AutomaticPageSegmentationWithOrientation, ocrSettings, cancellationToken);
            foreach (var (number, page) in retry)
            {
                if (page.IsAccepted && pages.TryGetValue(number, out var first) && !first.IsAccepted)
                {
                    pages[number] = page;
                }
            }
        }

        var accepted = pages.OrderBy(page => page.Key).Select(page => page.Value).Where(page => page.IsAccepted).ToList();
        logger.LogInformation(
            "Extracted image document {DocumentId}: {PageCount} pages with text, {AcceptedCount} OCR results used, {RejectedCount} rejected as unreadable",
            documentId,
            pages.Count,
            accepted.Count,
            pages.Count - accepted.Count);

        if (accepted.Count == 0)
        {
            var reason = pages.Values.Select(page => page.RejectionReason).FirstOrDefault() ?? "no text recognized";
            throw new InvalidOperationException($"No readable text was found in the image ({reason}).");
        }

        var text = JoinBlocks(accepted.Select(page => page.Text));
        var markdown = BuildMarkdown(() =>
        {
            var blocks = new List<(string Markdown, bool IsListItem)>();
            foreach (var page in MarkdownFormatter.RemoveRepeatedPageLines(accepted.Select(page => page.Text).ToList()))
            {
                MarkdownFormatter.AppendPlainText(blocks, page);
            }

            return MarkdownFormatter.Join(blocks);
        }, text, documentId);

        return new ExtractedDocumentDto { Text = text, Markdown = markdown, OcrShare = 1 };
    }

    // Tesseract numbers blocks, paragraphs and lines per page, so the words are split by page before OcrTextBuilder.
    private async Task<Dictionary<int, OcrTextResultDto>> RecognizeImagePagesAsync(
        byte[] image,
        int pageSegmentationMode,
        KnowledgeBaseOcrSettings ocrSettings,
        CancellationToken cancellationToken)
    {
        var response = await tesseractService.RecognizeAsync(new RecognizeRequestDto
        {
            Image = image,
            Languages = ocrSettings.Languages,
            PageSegmentationMode = pageSegmentationMode
        }, cancellationToken);

        return response.Words
            .GroupBy(word => word.PageNumber)
            .ToDictionary(page => page.Key, page => OcrTextBuilder.Build(page.ToList(), ocrSettings));
    }

    private async Task<ExtractedDocumentDto> ExtractPdfAsync(Stream fileStream, Guid documentId, CancellationToken cancellationToken)
    {
        var ocrSettings = options.Value.Ocr;
        var pages = DocumentTextExtractor.ReadPdfPages(fileStream);
        if (!ocrSettings.Enabled)
        {
            var layerOnly = pages.Select(page => new PdfPageTextDto { Text = page.Text, UsesTextLayer = true }).ToList();
            return ToExtractedPdf(pages, layerOnly, documentId);
        }

        var pageTexts = await ExtractPdfPagesWithOcrAsync(fileStream, pages, ocrSettings, cancellationToken);
        var extracted = ToExtractedPdf(pages, pageTexts, documentId);
        var text = extracted.Text;
        var ocrErrors = pageTexts.SelectMany(page => page.OcrErrors).ToList();

        logger.LogInformation(
            "Extracted PDF document {DocumentId}: {PageCount} pages, {WholePageOcrCount} OCR'ed as whole pages, {AcceptedCount} OCR results used, {RejectedCount} rejected as unreadable, {DuplicateCount} skipped as duplicates",
            documentId,
            pages.Count,
            pageTexts.Count(page => page.IsWholePageOcr),
            pageTexts.Sum(page => page.OcrAccepted),
            pageTexts.Sum(page => page.OcrRejected),
            pageTexts.Sum(page => page.OcrDuplicates));

        if (ocrErrors.Count > 0)
        {
            logger.LogWarning("OCR failed {ErrorCount} time(s) for PDF document {DocumentId}. First error: {Error}", ocrErrors.Count, documentId, ocrErrors[0]);
        }

        if (string.IsNullOrWhiteSpace(text) && ocrErrors.Count > 0)
        {
            throw new InvalidOperationException($"No extractable text was found in the file, and OCR failed: {ocrErrors[0]}");
        }

        return extracted;
    }

    private ExtractedDocumentDto ToExtractedPdf(List<PdfPageDto> pages, IReadOnlyList<PdfPageTextDto> pageTexts, Guid documentId)
    {
        var text = JoinBlocks(pageTexts.Select(page => page.Text));
        var ocrCharacters = pageTexts.Sum(page => page.OcrTexts.Sum(ocrText => ocrText.Length));
        return new ExtractedDocumentDto
        {
            Text = text,
            Markdown = BuildMarkdown(() => PdfMarkdownConverter.Render(pages, pageTexts), text, documentId),
            OcrShare = text.Length == 0 ? 0 : Math.Min(1, (double)ocrCharacters / text.Length)
        };
    }

    private static string JoinBlocks(IEnumerable<string> blocks) =>
        string.Join(Environment.NewLine + Environment.NewLine, blocks.Select(block => block.Trim()).Where(block => block.Length > 0));

    private async Task<PdfPageTextDto[]> ExtractPdfPagesWithOcrAsync(
        Stream pdfStream,
        List<PdfPageDto> pages,
        KnowledgeBaseOcrSettings ocrSettings,
        CancellationToken cancellationToken)
    {
        var parallelism = ocrSettings.MaxParallelism > 0 ? ocrSettings.MaxParallelism : Math.Max(1, Environment.ProcessorCount / 2);

        // Caps both the Tesseract processes running and the rendered pages held in memory. Not disposed,
        // because OCR tasks still release it if this loop is cancelled.
        var throttle = new SemaphoreSlim(parallelism);
        var seenImageHashes = new HashSet<string>();
        var pageTasks = new List<Task<PdfPageTextDto>>(pages.Count);

        foreach (var page in pages)
        {
            var textQuality = TextQuality.Analyze(page.Text);
            var hasText = textQuality.LetterOrDigitCount >= ocrSettings.MinCharacters;
            var isGarbled = !string.IsNullOrWhiteSpace(page.Text) && TextQuality.IsGarbled(textQuality);

            // Scanned, or the fonts give garbage: OCR the whole page. Otherwise only the images that may hold text.
            List<RectangleF?> regions = !hasText || isGarbled
                ? [null]
                : SelectImageRegions(page, textQuality, seenImageHashes);

            if (regions.Count == 0)
            {
                pageTasks.Add(Task.FromResult(new PdfPageTextDto { Text = page.Text, UsesTextLayer = true }));
                continue;
            }

            await throttle.WaitAsync(cancellationToken);
            var dpi = PdfPageRenderer.GetEffectiveDpi(page, ocrSettings.RenderDpi);
            List<byte[]> images;
            try
            {
                // Rendering runs here, one page at a time (PDFium is single-threaded), while earlier pages are OCR'ed.
                using var bitmap = PdfPageRenderer.Render(pdfStream, page, dpi);
                images = regions.Select(region => PdfPageRenderer.EncodePng(bitmap, page, region)).ToList();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throttle.Release();
                pageTasks.Add(Task.FromResult(new PdfPageTextDto
                {
                    Text = isGarbled ? string.Empty : page.Text,
                    UsesTextLayer = !isGarbled,
                    OcrErrors = [$"Page {page.Number} could not be rendered: {exception.Message}"]
                }));
                continue;
            }

            pageTasks.Add(OcrPageAsync(page, images, dpi, isWholePage: regions[0] is null, isGarbled, ocrSettings, throttle, cancellationToken));
        }

        return await Task.WhenAll(pageTasks);
    }

    private static List<RectangleF?> SelectImageRegions(PdfPageDto page, TextQualityDto textQuality, HashSet<string> seenImageHashes)
    {
        var pageArea = page.Width * page.Height;
        var regions = new List<RectangleF?>();
        foreach (var image in page.Images)
        {
            var bounds = image.Bounds;
            if (bounds.Width < MinOcrImageWidth || bounds.Height < MinOcrImageHeight)
            {
                continue;
            }

            if (textQuality.LetterOrDigitCount >= PlentyOfTextCharacters && bounds.Width * bounds.Height >= pageArea * FullPageImageShare)
            {
                continue;
            }

            // The same image repeated on many pages (a logo, a header) is only OCR'ed once.
            if (seenImageHashes.Add(image.ContentHash))
            {
                regions.Add(bounds);
            }
        }

        return regions;
    }

    private async Task<PdfPageTextDto> OcrPageAsync(
        PdfPageDto page,
        List<byte[]> images,
        int dpi,
        bool isWholePage,
        bool isGarbled,
        KnowledgeBaseOcrSettings ocrSettings,
        SemaphoreSlim throttle,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = new PdfPageTextDto { IsWholePageOcr = isWholePage, UsesTextLayer = !isWholePage };
            var parts = new List<string>();
            if (!isWholePage)
            {
                parts.Add(page.Text);
            }

            foreach (var image in images.Where(image => image.Length > 0))
            {
                OcrTextResultDto ocr;
                try
                {
                    ocr = await RecognizeTextAsync(image, dpi, AutomaticPageSegmentation, ocrSettings, cancellationToken);

                    // A scan fed upside down reads as garbage and gets rejected. Orientation detection turns it the
                    // right way round. Blank pages (no words at all) are not worth the extra run.
                    if (isWholePage && !ocr.IsAccepted && ocr.RecognizedWordCount > 0)
                    {
                        var retry = await RecognizeTextAsync(image, dpi, AutomaticPageSegmentationWithOrientation, ocrSettings, cancellationToken);
                        if (retry.IsAccepted)
                        {
                            ocr = retry;
                        }
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    result.OcrErrors.Add($"Page {page.Number}: {exception.Message}");
                    continue;
                }

                if (!ocr.IsAccepted)
                {
                    result.OcrRejected++;
                    logger.LogDebug("OCR text on page {PageNumber} rejected: {Reason}", page.Number, ocr.RejectionReason);
                    continue;
                }

                if (!isWholePage && TextQuality.IsMostlyContainedIn(ocr.Text, page.Text))
                {
                    result.OcrDuplicates++;
                    continue;
                }

                result.OcrAccepted++;
                result.OcrTexts.Add(ocr.Text);
                parts.Add(ocr.Text);
            }

            // OCR gave nothing usable for a page without real text: keep the little text it has (a page number,
            // a stamp), but never a garbled layer.
            if (isWholePage && result.OcrAccepted == 0 && !isGarbled)
            {
                result.UsesTextLayer = true;
                parts.Add(page.Text);
            }

            result.Text = JoinBlocks(parts);
            return result;
        }
        finally
        {
            throttle.Release();
        }
    }

    private async Task<OcrTextResultDto> RecognizeTextAsync(
        byte[] image,
        int dpi,
        int pageSegmentationMode,
        KnowledgeBaseOcrSettings ocrSettings,
        CancellationToken cancellationToken)
    {
        var response = await tesseractService.RecognizeAsync(new RecognizeRequestDto
        {
            Image = image,
            Languages = ocrSettings.Languages,
            Dpi = dpi,
            PageSegmentationMode = pageSegmentationMode
        }, cancellationToken);

        return OcrTextBuilder.Build(response.Words, ocrSettings);
    }
}
