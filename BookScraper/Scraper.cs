using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;

public sealed class Scraper : IDisposable
{
    private readonly ILogger<Scraper> logger;
    private readonly IHostApplicationLifetime hostApplicationLifetime;

    private HttpClient httpClient = new();

    private string baseUrl = "http://books.toscrape.com";
    private string rootDirPath = default!;

    private string outputFolder = "Output";

    private string errorFilePath = "failedDownloadsUris.txt";

    private string currentUrl = default!;
    private Stack<string> history = new Stack<string>();

    private HashSet<string> readPages = new HashSet<string>();
    private HashSet<string> failedDownloadsUris = new HashSet<string>();

    public Scraper(ILogger<Scraper> logger, IHostApplicationLifetime hostApplicationLifetime)
    {
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

            await ScrapeDocument(startPageUrl, cancellationToken);

            var elapsedTime = Stopwatch.GetElapsedTime(timestampStart);

            var hasErrors = failedDownloadsUris.Any();

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

        await File.WriteAllLinesAsync(errorFilePath, failedDownloadsUris, cancellationToken);

        logger.LogInformation($"Please check: {errorFilePath}");
    }

    private async Task ScrapeDocument(string url, CancellationToken cancellationToken)
    {
        var destFilePath = GetPath(url); 

        if (readPages.Contains(url))
        {
            logger.LogInformation($"Already exists: {url}");

            return;
        }

        readPages.Add(url);

        NavigateTo(url);

        logger.LogInformation($"Downloading: {url}");

        Stream stream;
        try
        {
            stream = await DownloadFileAsStream(url, cancellationToken);
        }
        catch (HttpRequestException exc)
        {
            failedDownloadsUris.Add(url);
            logger.LogError(exc, $"Failed to download: {url}");
            return;
        }

        var document = await ParseDocument(url, stream, cancellationToken);

        if (document is null)
        {
            logger.LogInformation("No content");

            GoBack();
            return;
        }

        stream.Seek(0, SeekOrigin.Begin);

        CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath, cancellationToken);

        logger.LogInformation($"Processing document");

        await ProcessScripts(document, cancellationToken);

        await ProcessLinks(document, cancellationToken);

        await ProcessImages(document, cancellationToken);

        await ProcessAnchors(document, cancellationToken);

        GoBack();
    }

    private void NavigateTo(string url)
    {
        if (currentUrl is not null)
        {
            history.Push(currentUrl);
        }
        currentUrl = url;

        logger.LogInformation($"Navigated to: {currentUrl}");
    }

    private void GoBack()
    {
        logger.LogInformation($"Leaving: {currentUrl}");

        currentUrl = history.Count > 0 ? history.Pop() : null!;
    }

    #region Process Page Elements

    private string AsAbsoluteUrl(string relUrl)
    {
        return UrlHelpers.AsAbsoluteUrl(baseUrl, currentUrl, relUrl);
    }

    private async Task ProcessScripts(IDocument document, CancellationToken cancellationToken)
    {
        var scriptElements = document.QuerySelectorAll("script");

        var scriptElementSrcs = scriptElements
            .Select(scriptElement => scriptElement.GetAttribute("src")!)
            .Where(scriptElementSrc => scriptElementSrc is not null);

        foreach (var scriptElementSrc in scriptElementSrcs)
        {
            await ProcessScriptSrc(scriptElementSrc, cancellationToken);
        }
    }

    private async Task ProcessScriptSrc(string scriptElementSrc, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Found script: {scriptElementSrc}");

        // INFO: Ignore files not hosted on the server

        if (scriptElementSrc.StartsWith("http"))
            return;

        var uri = AsAbsoluteUrl(scriptElementSrc);

        var destFilePath = GetPath(uri);

        if (File.Exists(destFilePath))
        {
            logger.LogInformation($"Already exists: {scriptElementSrc}");
            return;
        }

        Stream stream;
        try
        {
            stream = await DownloadFileAsStream(uri, cancellationToken);
        }
        catch (HttpRequestException exc)
        {
            failedDownloadsUris.Add(uri);
            logger.LogError(exc, $"Failed to download: {uri}");
            return;
        }

        CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath, cancellationToken);

        logger.LogInformation($"Saved: {scriptElementSrc}");
    }

    private async Task ProcessLinks(IDocument document, CancellationToken cancellationToken)
    {
        var links = document.QuerySelectorAll("link");

        var linkHrefs = links
            .Select(link => link.GetAttribute("href")!)
            .Where(linkHref => linkHref is not null);

        foreach (var linkHref in linkHrefs)
        {
            await ProcessLinkHref(linkHref, cancellationToken);
        }
    }

    private async Task ProcessLinkHref(string linkSrc, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Found link: {linkSrc}");

        var uri = AsAbsoluteUrl(linkSrc);

        var destFilePath = GetPath(uri);

        if (File.Exists(destFilePath))
        {
            logger.LogInformation($"Already exists: {linkSrc}");
            return;
        }

        Stream stream;
        try
        {
            stream = await DownloadFileAsStream(uri, cancellationToken);
        }
        catch (HttpRequestException exc)
        {
            failedDownloadsUris.Add(uri);
            logger.LogError(exc, $"Failed to download: {uri}");
            return;
        }

        CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath, cancellationToken);

        logger.LogInformation($"Saved: {linkSrc}");
    }

    private async Task ProcessAnchors(IDocument document, CancellationToken cancellationToken)
    {
        // INFO: Select links in Sidebar (Category links) + all links that are not on the Book page (i.e. not links in the "Recently viewed" section).

        var anchors = document.QuerySelectorAll(".sidebar a, :not(article.product_page) a"); // a

        var anchorHrefs = anchors
            .Select(anchor => anchor.GetAttribute("href")!)
            .Where(anchorHref => anchorHref is not null);

        foreach (var anchorHref in anchorHrefs)
        {
            logger.LogInformation($"Found anchor: {anchorHref}");

            var uri = AsAbsoluteUrl(anchorHref);

            await ScrapeDocument(uri, cancellationToken);
        }
    }

    private async Task ProcessImages(IDocument document, CancellationToken cancellationToken)
    {
        var imgs = document.QuerySelectorAll("img");

        var imgSrcs = imgs
            .Select(img => img.GetAttribute("src")!)
            .Where(imgSrc => imgSrc is not null);

        foreach (var imgSrc in imgSrcs)
        {
            await ProcessImageSrc(imgSrc, cancellationToken);
        }
    }

    private async Task ProcessImageSrc(string imgSrc, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Found image: {imgSrc}");

        var uri = AsAbsoluteUrl(imgSrc);

        var destFilePath = GetPath(uri);

        if (File.Exists(destFilePath))
        {
            logger.LogInformation($"Already exists: {imgSrc}");
            return;
        }

        Stream stream;
        try
        {
            stream = await DownloadFileAsStream(uri, cancellationToken);
        }
        catch (HttpRequestException exc)
        {
            failedDownloadsUris.Add(uri);
            logger.LogError(exc, $"Failed to download: {uri}");
            return;
        }

        CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath, cancellationToken);

        logger.LogInformation($"Saved: {imgSrc}");
    }

    #endregion

    async Task<Stream> DownloadFileAsStream(string url, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Downloading: {url}");

        var stream = await httpClient.GetStreamAsync(url, cancellationToken);

        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);

        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream;
    }

    async Task<IDocument?> ParseDocument(string url, Stream stream, CancellationToken cancellationToken)
    {
        var config = Configuration.Default.WithDefaultLoader();

        var context = BrowsingContext.New(config);

        return await context.OpenAsync(res => res
            .Content(stream)
            .Address(url), cancellationToken);
    }

    private static string GetPath(string uri)
    {
        return new Uri(uri).LocalPath[1..];
    }

    private static void CreateDirectory(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath!);

        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
