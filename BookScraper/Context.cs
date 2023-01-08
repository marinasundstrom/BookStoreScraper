namespace BookScraper;

public class Context
{
    public Context(string baseUrl, string currentUrl)
    {
        this.BaseUrl = baseUrl;
        this.CurrentUrl = currentUrl;
    }

    public string BaseUrl { get; set; } = default!;

    public string CurrentUrl { get; set; } = default!;

    public string AsAbsoluteUrl(string relUrl)
    {
        return UrlHelpers.AsAbsoluteUrl(BaseUrl, CurrentUrl, relUrl);
    }
}
