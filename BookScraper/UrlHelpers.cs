namespace BookScraper;

public static class UrlHelpers
{
    private const string Value = "http";

    public static string AsAbsoluteUrl(string baseUrl, string currentUrl, string relUrl)
    {
        if (relUrl.StartsWith(Value))
        {
            return relUrl;
        }

        var directory = Path.GetDirectoryName(new Uri(currentUrl).LocalPath);
        var url = $"{baseUrl}{directory}";

        return new Uri(Path.Combine(url, relUrl)).AbsoluteUri;
    }
}