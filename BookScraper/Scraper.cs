using System;
using System.Reflection;
using AngleSharp;
using AngleSharp.Browser.Dom;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;

public sealed class Scraper : IDisposable
{
    private readonly ILogger<Scraper> logger;
    private readonly IHostApplicationLifetime hostApplicationLifetime;

    private HttpClient httpClient = new();

    private string baseUrl = "http://books.toscrape.com";
    private string rootDirPath;

    private string currentUrl;
    private Stack<string> history = new Stack<string>();

    private HashSet<string> readPages = new HashSet<string>();

    public Scraper(ILogger<Scraper> logger, IHostApplicationLifetime hostApplicationLifetime)
    {
        this.logger = logger;
        this.hostApplicationLifetime = hostApplicationLifetime;

        SetupDirectoryStructure();
    }

    private void SetupDirectoryStructure()
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var outputFolder = "Output";

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
        await ScrapeDocument($"{baseUrl}/index.html");

        hostApplicationLifetime.StopApplication();
    }

    private async Task ScrapeDocument(string url)
    {
        var uri = new Uri(url);

        var outputPath = uri.LocalPath[1..];

        if (readPages.Contains(url))
        {
            logger.LogInformation($"Already exists {url}");

            return;
        }

        readPages.Add(url);

        Navigate(url);

        logger.LogInformation($"Downloading {url}");

        var stream = await DownloadFileAsStream(url);

        var document = await ParseDocument(url, stream);

        if (document is null)
        {
            PopStack();
            return;
        }

        stream.Seek(0, SeekOrigin.Begin);

        string dir;
        if (!string.IsNullOrEmpty(dir = Path.GetDirectoryName(outputPath!)))
        {
            Directory.CreateDirectory(dir);
        }

        await stream.WriteToFileAsync(outputPath);

        logger.LogInformation($"Processing document");

        await ProcessScripts(document);

        await ProcessLinks(document);

        await ProcessImage(document);

        await ProcessAnchors(document);

        PopStack();
    }

    private void Navigate(string url)
    {
        if (currentUrl is not null)
        {
            history.Push(currentUrl);
        }
        currentUrl = url;

        logger.LogInformation($"Navigated to: {currentUrl}");
    }

    private void PopStack()
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

        foreach (var scriptElement in scriptElements)
        {
            var scriptElementSrc = scriptElement.GetAttribute("src");

            if (scriptElementSrc is null)
            {
                continue;
            }

            logger.LogInformation($"Found script: {scriptElementSrc}");

            if (scriptElementSrc.StartsWith("http")) continue;

            var uri = AsAbsoluteUrl(scriptElementSrc);

            var f = GetPath(uri);

            if (File.Exists(f))
            {
                logger.LogInformation($"Already exists: {scriptElementSrc}");
                continue;
            }

            Stream stream = await DownloadFileAsStream(uri);

            Directory.CreateDirectory(Path.GetDirectoryName(f));

            await stream.WriteToFileAsync(f);

            logger.LogInformation($"Saved: {scriptElementSrc}");
        }
    }

    private static string GetPath(string uri)
    {
        return new Uri(uri).LocalPath[1..];
    }

    private async Task ProcessLinks(IDocument document)
    {
        var links = document.QuerySelectorAll("link");

        foreach (var link in links)
        {
            var linkSrc = link.GetAttribute("href");

            if (linkSrc is null)
            {
                continue;
            }

            logger.LogInformation($"Found link: {linkSrc}");

            var uri = AsAbsoluteUrl(linkSrc);

            var f = GetPath(uri);

            if (File.Exists(f))
            {
                logger.LogInformation($"Already exists: {linkSrc}");
                continue;
            }

            Stream stream = await DownloadFileAsStream(uri);

            Directory.CreateDirectory(Path.GetDirectoryName(f));

            await stream.WriteToFileAsync(f);

            logger.LogInformation($"Saved: {linkSrc}");
        }
    }

    private async Task ProcessAnchors(IDocument document)
    {
        var anchors = document.QuerySelectorAll(".sidebar a, :not(article.product_page) a"); // a

        foreach (var anchor in anchors)
        {
            var anchorHref = anchor.GetAttribute("href");

            if (anchorHref is null)
            {
                continue;
            }

            logger.LogInformation($"Found anchor: {anchorHref}");

            await ScrapeDocument(AsAbsoluteUrl(anchorHref));
        }
    }

    private async Task ProcessImage(IDocument document)
    {
        var imgs = document.QuerySelectorAll("img");
        foreach (var img in imgs)
        {
            var imgSrc = img.GetAttribute("src");

            if (imgSrc is null)
            {
                continue;
            }

            logger.LogInformation($"Found image: {imgSrc}");

            var uri = AsAbsoluteUrl(imgSrc);

            var f = GetPath(uri);

            if (File.Exists(f))
            {
                logger.LogInformation($"Already exists: {imgSrc}");
                continue;
            }

            Stream stream = await DownloadFileAsStream(uri);

            Directory.CreateDirectory(Path.GetDirectoryName(f));

            await stream.WriteToFileAsync(f);

            logger.LogInformation($"Saved: {imgSrc}");
        }
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
