using System;
using Microsoft.Extensions.Logging;

namespace BookScraper.Processors;

public sealed class LinkProcessor
{
    private readonly MyHttpClient httpClient;
    private readonly ILogger<DocumentProcessor> logger;

    public LinkProcessor(MyHttpClient httpClient, ILogger<DocumentProcessor> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task ProcessLinkHref(Context context, string linkSrc, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Found link: {linkSrc}");

        var uri = context.AsAbsoluteUrl(linkSrc);

        var destFilePath = Utils.GetPath(uri);

        if (File.Exists(destFilePath))
        {
            logger.LogInformation($"Already exists: {uri}");
            return;
        }

        try
        {
            await httpClient.DownloadFile(uri, cancellationToken);

            logger.LogInformation($"Saved: {linkSrc}");
        }
        catch (Exception exc) when (exc is not TaskCanceledException)
        {
            logger.LogError(exc, $"Failed to download: {uri}");

            return;
        }
    }
}

