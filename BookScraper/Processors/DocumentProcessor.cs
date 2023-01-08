using System;
using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;

namespace BookScraper.Processors;

public sealed class DocumentProcessor
{
    private readonly MyHttpClient httpClient;
    private readonly NavigationManager navigationManager;

    private readonly ImgProcessor imgProcessor;
    private readonly LinkProcessor linkProcessor;
    private readonly ScriptProcessor scriptProcessor;
    private readonly ErrorSink errorSink;
    private readonly ILogger<DocumentProcessor> logger;

    private readonly HashSet<string> readPages = new HashSet<string>();

    public DocumentProcessor(
        MyHttpClient httpClient,
        NavigationManager navigationManager,
        ImgProcessor imgProcessor,
        LinkProcessor linkProcessor,
        ScriptProcessor scriptProcessor,
        ErrorSink errorSink,
        ILogger<DocumentProcessor> logger)
    {
        this.httpClient = httpClient;
        this.navigationManager = navigationManager;
        this.imgProcessor = imgProcessor;
        this.linkProcessor = linkProcessor;
        this.scriptProcessor = scriptProcessor;
        this.errorSink = errorSink;
        this.logger = logger;
    }

    public async Task ScrapeDocument(string url, CancellationToken cancellationToken)
    {
        var uri = new Uri(url);
        var baseUrl = $"{uri.Scheme}://{uri.Host}";
        var context = new Context(baseUrl, url);

        await ScrapeDocument(context, cancellationToken);
    }

    private async Task ScrapeDocument(Context context, CancellationToken cancellationToken)
    {
        var url = context.CurrentUrl;
        var destFilePath = Utils.GetPath(url);

        if (readPages.Contains(url))
        {
            logger.LogInformation($"Already exists: {url}");

            return;
        }

        readPages.Add(url);
        navigationManager.NavigateTo(url);

        logger.LogInformation($"Downloading: {url}");

        Stream stream;
        try
        {
            stream = await httpClient.GetStream(url, cancellationToken);
        }
        catch (HttpRequestException exc)
        {
            errorSink.AddFile(url);
            logger.LogError(exc, $"Failed to download: {url}");
            return;
        }

        var document = await ParseDocument(url, stream, cancellationToken);

        if (document is null)
        {
            logger.LogInformation("No content");

            navigationManager.GoBack();
            return;
        }

        stream.Seek(0, SeekOrigin.Begin);

        Utils.CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath, cancellationToken);

        logger.LogInformation($"Processing document");

        await ProcessHtml(context, document, cancellationToken);

        navigationManager.GoBack();
    }

    private async Task ProcessHtml(Context context, IDocument document, CancellationToken cancellationToken)
    {
        await ProcessScripts(context, document, cancellationToken);

        await ProcessLinks(context, document, cancellationToken);

        await ProcessImages(context, document, cancellationToken);

        await ProcessAnchors(context, document, cancellationToken);
    }


    #region Process Page Elements

    private async Task ProcessScripts(Context context, IDocument document, CancellationToken cancellationToken)
    {
        var scriptElements = document.QuerySelectorAll("script");

        var scriptElementSrcs = scriptElements
            .Select(scriptElement => scriptElement.GetAttribute("src")!)
            .Where(scriptElementSrc => scriptElementSrc is not null);

        foreach (var scriptElementSrc in scriptElementSrcs)
        {
            await scriptProcessor.ProcessScriptSrc(context, scriptElementSrc, cancellationToken);
        }
    }

    private async Task ProcessLinks(Context context, IDocument document, CancellationToken cancellationToken)
    {
        var links = document.QuerySelectorAll("link");

        var linkHrefs = links
            .Select(link => link.GetAttribute("href")!)
            .Where(linkHref => linkHref is not null);

        foreach (var linkHref in linkHrefs)
        {
            await linkProcessor.ProcessLinkHref(context, linkHref, cancellationToken);
        }
    }

    private async Task ProcessAnchors(Context context, IDocument document, CancellationToken cancellationToken)
    {
        // INFO: Select links in Sidebar (Category links) + all links that are not on the Book page (i.e. not links in the "Recently viewed" section).

        var anchors = document.QuerySelectorAll(".sidebar a, :not(article.product_page) a"); // a

        var anchorHrefs = anchors
            .Select(anchor => anchor.GetAttribute("href")!)
            .Where(anchorHref => anchorHref is not null);

        foreach (var anchorHref in anchorHrefs)
        {
            logger.LogInformation($"Found anchor: {anchorHref}");

            var uri = context.AsAbsoluteUrl(anchorHref);

            var newContext = new Context(context.BaseUrl, uri);

            await this.ScrapeDocument(newContext, cancellationToken);
        }
    }

    private async Task ProcessImages(Context context, IDocument document, CancellationToken cancellationToken)
    {
        var imgs = document.QuerySelectorAll("img");

        var imgSrcs = imgs
            .Select(img => img.GetAttribute("src")!)
            .Where(imgSrc => imgSrc is not null);

        foreach (var imgSrc in imgSrcs)
        {
            await imgProcessor.ProcessImageSrc(context, imgSrc, cancellationToken);
        }
    }

    #endregion

    async Task<IDocument?> ParseDocument(string url, Stream stream, CancellationToken cancellationToken)
    {
        var config = Configuration.Default.WithDefaultLoader();

        var context = BrowsingContext.New(config);

        return await context.OpenAsync(res => res
            .Content(stream)
            .Address(url), cancellationToken);
    }
}

