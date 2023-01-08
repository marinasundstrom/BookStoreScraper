namespace BookScraper;

public static class UrlHelpers
{
    public static string AsAbsoluteUrl(string baseUrl, string currentUrl, string relUrl)
    {
        if (relUrl.StartsWith("http"))
        {
            return relUrl;
        }

        var directory = Path.GetDirectoryName(new Uri(currentUrl).LocalPath);
        var url = $"{baseUrl}{directory}";

        return new Uri(Path.Combine(url, relUrl)).AbsoluteUri;
    }
}