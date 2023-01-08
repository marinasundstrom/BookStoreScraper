using System.Diagnostics;
using System.Reflection;
using AngleSharp;
using AngleSharp.Dom;
using BookScraper.Processors;
using Microsoft.Extensions.Logging;

namespace BookScraper;

public sealed class Scraper : IDisposable
{
    private readonly DocumentProcessor documentProcessor;
    private readonly ErrorSink errorSink;
    private readonly ILogger<Scraper> logger;
    private readonly IHostApplicationLifetime hostApplicationLifetime;

    private HttpClient httpClient = new();

    private string baseUrl = "http://books.toscrape.com";
    private string rootDirPath = default!;

    private string outputFolder = "Output";

    private string errorFilePath = "failedDownloadsUris.txt";

    public Scraper(
        DocumentProcessor documentProcessor,
        ErrorSink errorSink,
        ILogger<Scraper> logger,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        this.documentProcessor = documentProcessor;
        this.errorSink = errorSink;
        this.logger = logger;
        this.hostApplicationLifetime = hostApplicationLifetime;

        SetupDirectoryStructure();
    }

    private void SetupDirectoryStructure()
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        rootDirPath = Path.Combine(assemblyLocation, outputFolder);

        try
        {
            Directory.Delete(rootDirPath, true);
            Directory.CreateDirectory(rootDirPath);
        }
        catch (IOException) { throw; }

        Environment.CurrentDirectory = rootDirPath;
    }

    public async Task Scrape(CancellationToken cancellationToken = default)
    {
        try
        {
            string startPageUrl = $"{baseUrl}/index.html";

            var timestampStart = Stopwatch.GetTimestamp();

            await documentProcessor.ScrapeDocument(startPageUrl, cancellationToken);

            var elapsedTime = Stopwatch.GetElapsedTime(timestampStart);

            var hasErrors = errorSink.HasErrors;

            if (hasErrors)
            {
                logger.LogInformation($"Completed with errors in {elapsedTime}");

                await HandleErrors(elapsedTime, cancellationToken);
            }
            else
            {
                logger.LogInformation($"Completed in {elapsedTime}");
            }

            hostApplicationLifetime.StopApplication();
        }
        catch (TaskCanceledException exc)
        {
            logger.LogInformation("The program was cancelled.");
        }
    }

    private async Task HandleErrors(TimeSpan elapsedTime, CancellationToken cancellationToken)
    {
        try
        {
            File.Delete(errorFilePath);
        }
        catch { }

        await File.WriteAllLinesAsync(errorFilePath, errorSink.Errors, cancellationToken);

        logger.LogInformation($"Please check: {errorFilePath}");
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
