using System;
using Microsoft.Extensions.Logging;

namespace BookScraper.Processors;

public sealed class ScriptProcessor
{
    private readonly MyHttpClient httpClient;
    private readonly ILogger<DocumentProcessor> logger;

    public ScriptProcessor(MyHttpClient httpClient, ILogger<DocumentProcessor> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task ProcessScriptSrc(Context context, string scriptElementSrc, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Found script: {scriptElementSrc}");

        // INFO: Ignore files not hosted on the server

        if (scriptElementSrc.StartsWith("http"))
            return;

        var uri = context.AsAbsoluteUrl(scriptElementSrc);

        var destFilePath = Utils.GetPath(uri);

        if (File.Exists(destFilePath))
        {
            logger.LogInformation($"Already exists: {uri}");
            return;
        }

        await httpClient.DownloadFile(uri, cancellationToken);

        logger.LogInformation($"Saved: {scriptElementSrc}");
    }

}

