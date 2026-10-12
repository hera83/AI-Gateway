using System.Drawing;
using api.Managers.KnowledgeBase.Dtos;
using api.Services.Tesseract.Dtos.Recognize;

namespace api.Managers.KnowledgeBase;

// Text extraction for uploads. A PDF page keeps its own text layer when it is real text, and Tesseract OCR
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

    private async Task<string> ExtractTextAsync(Stream fileStream, string fileName, Guid documentId, CancellationToken cancellationToken)
    {
        if (!DocumentTextExtractor.IsPdf(fileName))
        {
            return await DocumentTextExtractor.ExtractTextAsync(fileStream, fileName, cancellationToken);
        }

        var ocrSettings = options.Value.Ocr;
        var pages = DocumentTextExtractor.ReadPdfPages(fileStream);
        if (!ocrSettings.Enabled)
        {
            return JoinBlocks(pages.Select(page => page.Text));
        }

        var pageTexts = await ExtractPdfPagesWithOcrAsync(fileStream, pages, ocrSettings, cancellationToken);
        var text = JoinBlocks(pageTexts.Select(page => page.Text));
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

        return text;
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
                pageTasks.Add(Task.FromResult(new PdfPageTextDto { Text = page.Text }));
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
            var result = new PdfPageTextDto { IsWholePageOcr = isWholePage };
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
                parts.Add(ocr.Text);
            }

            // OCR gave nothing usable for a page without real text: keep the little text it has (a page number,
            // a stamp), but never a garbled layer.
            if (isWholePage && result.OcrAccepted == 0 && !isGarbled)
            {
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
