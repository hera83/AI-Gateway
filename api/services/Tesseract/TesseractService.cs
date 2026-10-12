using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using api.Services.Tesseract.Dtos.Recognize;
using api.Services.Tesseract.Interfaces;

namespace api.Services.Tesseract;

// Light pattern: runs the tesseract CLI as a child process and reads the "Tesseract" section on every call.
public class TesseractService(IConfiguration configuration, IHostEnvironment environment) : ITesseractService
{
    public async Task<RecognizeResponseDto> RecognizeAsync(RecognizeRequestDto request, CancellationToken cancellationToken = default)
    {
        var executablePath = ResolvePath(configuration["Tesseract:ExecutablePath"] is { Length: > 0 } path ? path : "tesseract");
        var tessdataPath = configuration["Tesseract:TessdataPath"] is { Length: > 0 } tessdata ? ResolvePath(tessdata) : null;
        var timeout = TimeSpan.FromSeconds(configuration.GetValue("Tesseract:TimeoutSeconds", 120));

        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // The image is piped in and the TSV (one row per word, with confidence) comes back on stdout, so no temp files.
        startInfo.ArgumentList.Add("stdin");
        startInfo.ArgumentList.Add("stdout");
        if (tessdataPath is not null)
        {
            startInfo.ArgumentList.Add("--tessdata-dir");
            startInfo.ArgumentList.Add(tessdataPath);
        }

        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add(request.Languages);
        startInfo.ArgumentList.Add("--psm");
        startInfo.ArgumentList.Add(request.PageSegmentationMode.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--dpi");
        startInfo.ArgumentList.Add(request.Dpi.ToString(CultureInfo.InvariantCulture));

        // Set directly instead of the "tsv" config file, which a custom tessdata folder usually lacks
        // (Tesseract then silently falls back to plain text).
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("tessedit_create_tsv=1");

        // Callers run several processes in parallel. Tesseract's own OpenMP threads would only compete with them.
        startInfo.Environment["OMP_THREAD_LIMIT"] = "1";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                $"Tesseract could not be started from '{executablePath}'. Check that it is installed and that Tesseract:ExecutablePath is correct.",
                exception);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);

            try
            {
                await process.StandardInput.BaseStream.WriteAsync(request.Image, timeoutSource.Token);
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // Tesseract exited before reading the whole image (e.g. a missing language). The exit code and stderr below say why.
            }

            await process.WaitForExitAsync(timeoutSource.Token);
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Tesseract failed with exit code {process.ExitCode}: {error.Trim()}");
            }

            return new RecognizeResponseDto { Words = ParseWords(output) };
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException($"Tesseract did not finish within {timeout.TotalSeconds:0} seconds.");
        }
    }

    // Relative paths (the bundled services/Tesseract/...) are resolved against the content root, like app_dbs/.
    // A bare program name such as "tesseract" is left alone, so the OS finds it on PATH.
    private string ResolvePath(string path) =>
        Path.IsPathRooted(path) || path.IndexOfAny(['/', '\\']) < 0
            ? path
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, path));

    // TSV columns: level page_num block_num par_num line_num word_num left top width height conf text.
    // Only level 5 rows are words. The other levels (page, block, paragraph, line) have conf -1 and no text.
    private static List<WordDto> ParseWords(string tsv)
    {
        var words = new List<WordDto>();
        foreach (var row in tsv.Split('\n').Skip(1))
        {
            var columns = row.TrimEnd('\r').Split('\t');
            if (columns.Length < 12 || columns[0] != "5" || string.IsNullOrWhiteSpace(columns[11]))
            {
                continue;
            }

            words.Add(new WordDto
            {
                BlockNumber = int.Parse(columns[2], CultureInfo.InvariantCulture),
                ParagraphNumber = int.Parse(columns[3], CultureInfo.InvariantCulture),
                LineNumber = int.Parse(columns[4], CultureInfo.InvariantCulture),
                WordNumber = int.Parse(columns[5], CultureInfo.InvariantCulture),
                Left = int.Parse(columns[6], CultureInfo.InvariantCulture),
                Top = int.Parse(columns[7], CultureInfo.InvariantCulture),
                Width = int.Parse(columns[8], CultureInfo.InvariantCulture),
                Height = int.Parse(columns[9], CultureInfo.InvariantCulture),
                Confidence = double.Parse(columns[10], CultureInfo.InvariantCulture),
                Text = columns[11]
            });
        }

        return words;
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Exited between the check and the kill.
        }
    }
}
