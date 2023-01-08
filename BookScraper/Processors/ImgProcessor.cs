using System;
using Microsoft.Extensions.Logging;

namespace BookScraper.Processors;

public sealed class ImgProcessor
{
    private readonly MyHttpClient httpClient;
    private readonly ILogger<DocumentProcessor> logger;

    public ImgProcessor(MyHttpClient httpClient, ILogger<DocumentProcessor> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task ProcessImageSrc(Context context, string imgSrc, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Found image: {imgSrc}");

        var uri = context.AsAbsoluteUrl(imgSrc);

        var destFilePath = Utils.GetPath(uri);

        if (File.Exists(destFilePath))
        {
            logger.LogInformation($"Already exists: {uri}");
            return;
        }

        await httpClient.DownloadFile(uri, cancellationToken);

        logger.LogInformation($"Saved: {imgSrc}");
    }

}

