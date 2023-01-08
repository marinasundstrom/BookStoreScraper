﻿using System;
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

    private string outputFolder = "Output";

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
        var destFilePath = GetPath(url); 

        if (readPages.Contains(url))
        {
            logger.LogInformation($"Already exists {url}");

            return;
        }

        readPages.Add(url);

        NavigateTo(url);

        logger.LogInformation($"Downloading {url}");

        var stream = await DownloadFileAsStream(url);

        var document = await ParseDocument(url, stream);

        if (document is null)
        {
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

        logger.LogInformation($"NavigateTod to: {currentUrl}");
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
            await ProcessScript(scriptElementSrc);
        }
    }

    private async Task ProcessScript(string scriptElementSrc)
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

        Stream stream = await DownloadFileAsStream(uri);

        CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath);

        logger.LogInformation($"Saved: {scriptElementSrc}");
    }

    private async Task ProcessLinks(IDocument document)
    {
        var links = document.QuerySelectorAll("link");

        var linkSrcs = links
            .Select(link => link.GetAttribute("href")!)
            .Where(linkSrc => linkSrc is not null);

        foreach (var linkSrc in linkSrcs)
        {
            await ProcessLink(linkSrc);
        }
    }

    private async Task ProcessLink(string linkSrc)
    {
        logger.LogInformation($"Found link: {linkSrc}");

        var uri = AsAbsoluteUrl(linkSrc);

        var destFilePath = GetPath(uri);

        if (File.Exists(destFilePath))
        {
            logger.LogInformation($"Already exists: {linkSrc}");
            return;
        }

        Stream stream = await DownloadFileAsStream(uri);

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
            await ProcessImage(imgSrc);
        }
    }

    private async Task ProcessImage(string imgSrc)
    {
        logger.LogInformation($"Found image: {imgSrc}");

        var uri = AsAbsoluteUrl(imgSrc);

        var destFilePath = GetPath(uri);

        if (File.Exists(destFilePath))
        {
            logger.LogInformation($"Already exists: {imgSrc}");
            return;
        }

        Stream stream = await DownloadFileAsStream(uri);

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
