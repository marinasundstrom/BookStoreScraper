using Microsoft.Extensions.Logging;

namespace BookScraper;

public class MyHttpClient
{
    private readonly HttpClient httpClient = new HttpClient();
    private readonly ILogger<MyHttpClient> logger;
    private HashSet<string> failedDownloadsUris = new HashSet<string>();

    public MyHttpClient(HttpClient httpClient, ILogger<MyHttpClient> logger)
    {
        //this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task DownloadFile(string uri, CancellationToken cancellationToken)
    {
        var destFilePath = Utils.GetPath(uri);

        Stream stream;
        try
        {
            stream = await GetStream(uri, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception exc)
        {
            failedDownloadsUris.Add(uri);
            logger.LogError(exc, $"Failed to download: {uri}");

            return;
        }

        Utils.CreateDirectory(destFilePath);

        await stream.WriteToFileAsync(destFilePath, cancellationToken);
    }

    public async Task<Stream> GetStream(string url, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Downloading: {url}");

        var stream = await httpClient.GetStreamAsync(url, cancellationToken);

        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);

        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream;
    }
}

