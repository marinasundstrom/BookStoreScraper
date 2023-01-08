using System;
using System.Diagnostics;
using System.Reflection;
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

    public async Task Scrape()
    {
        string startPageUrl = $"{baseUrl}/index.html";

        var timestampStart = Stopwatch.GetTimestamp();

        await ScrapeDocument(startPageUrl);

        var elapsedTime = Stopwatch.GetElapsedTime(timestampStart);

        var hasErrors = failedDownloadsUris.Any();

        if (hasErrors)
        {
            logger.LogInformation($"Completed with errors in {elapsedTime}");

            HandleErrors(elapsedTime);
        }
        else
        {
            logger.LogInformation($"Completed in {elapsedTime}");
        }

        hostApplicationLifetime.StopApplication();
    }

    private void HandleErrors(TimeSpan elapsedTime)
    {
        try
        {
            File.Delete(errorFilePath);
        }
        catch { }

        File.WriteAllLines(errorFilePath, failedDownloadsUris);

        logger.LogInformation($"Please check: {errorFilePath}");
    }

    private async Task ScrapeDocument(string url)
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
            stream = await DownloadFileAsStream(url);
        }
        catch (Exception exc)
        {
            failedDownloadsUris.Add(url);
            logger.LogError(exc, $"Failed to download: {url}");
            return;
        }

        var document = await ParseDocument(url, stream);

        if (document is null)
        {
            logger.LogInformation("No content");

            GoBack();
            return;
        }

        stream.Seek(0, SeekOrigin.Begin);

        CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath);

        logger.LogInformation($"Processing document");

        await ProcessScripts(document);

        await ProcessLinks(document);

        await ProcessImages(document);

        await ProcessAnchors(document);

        GoBack();
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

    private async Task ProcessScripts(IDocument document)
    {
        var scriptElements = document.QuerySelectorAll("script");

        var scriptElementSrcs = scriptElements
            .Select(scriptElement => scriptElement.GetAttribute("src")!)
            .Where(scriptElementSrc => scriptElementSrc is not null);

        foreach (var scriptElementSrc in scriptElementSrcs)
        {
            await ProcessScriptSrc(scriptElementSrc);
        }
    }

    private async Task ProcessScriptSrc(string scriptElementSrc)
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
            stream = await DownloadFileAsStream(uri);
        }
        catch (Exception exc)
        {
            failedDownloadsUris.Add(uri);
            logger.LogError(exc, $"Failed to download: {uri}");
            return;
        }

        CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath);

        logger.LogInformation($"Saved: {scriptElementSrc}");
    }

    private async Task ProcessLinks(IDocument document)
    {
        var links = document.QuerySelectorAll("link");

        var linkHrefs = links
            .Select(link => link.GetAttribute("href")!)
            .Where(linkHref => linkHref is not null);

        foreach (var linkHref in linkHrefs)
        {
            await ProcessLinkHref(linkHref);
        }
    }

    private async Task ProcessLinkHref(string linkSrc)
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
            stream = await DownloadFileAsStream(uri);
        }
        catch (Exception exc)
        {
            failedDownloadsUris.Add(uri);
            logger.LogError(exc, $"Failed to download: {uri}");
            return;
        }

        CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath);

        logger.LogInformation($"Saved: {linkSrc}");
    }

    private async Task ProcessAnchors(IDocument document)
    {
        // INFO: Select links in Sidebar (Category links) + all links that are not on the Book page (i.e. not links in the "Recently viewed" section).

        var anchors = document.QuerySelectorAll(".sidebar a, :not(article.product_page) a"); // a

        var anchorHrefs = anchors
            .Select(anchor => anchor.GetAttribute("href")!)
            .Where(anchorHref => anchorHref is not null);

        foreach (var anchorHref in anchorHrefs)
        {
            logger.LogInformation($"Found anchor: {anchorHref}");

            await ScrapeDocument(AsAbsoluteUrl(anchorHref));
        }
    }

    private async Task ProcessImages(IDocument document)
    {
        var imgs = document.QuerySelectorAll("img");

        var imgSrcs = imgs
            .Select(img => img.GetAttribute("src")!)
            .Where(imgSrc => imgSrc is not null);

        foreach (var imgSrc in imgSrcs)
        {
            await ProcessImageSrc(imgSrc);
        }
    }

    private async Task ProcessImageSrc(string imgSrc)
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
            stream = await DownloadFileAsStream(uri);
        }
        catch (Exception exc)
        {
            failedDownloadsUris.Add(uri);
            logger.LogError(exc, $"Failed to download: {uri}");
            return;
        }

        CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath);

        logger.LogInformation($"Saved: {imgSrc}");
    }

    #endregion

    async Task<Stream> DownloadFileAsStream(string url)
    {
        logger.LogInformation($"Downloading: {url}");

        var stream = await httpClient.GetStreamAsync(url);

        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);

        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream;
    }

    async Task<IDocument?> ParseDocument(string url, Stream stream)
    {
        var config = Configuration.Default.WithDefaultLoader();

        var context = BrowsingContext.New(config);

        return await context.OpenAsync(res => res
            .Content(stream)
            .Address(url));
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
