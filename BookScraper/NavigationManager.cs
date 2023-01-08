using Microsoft.Extensions.Logging;

namespace BookScraper;

public class NavigationManager
{
    private readonly Stack<string> history = new Stack<string>();
    private readonly ILogger<NavigationManager> logger;

    public string CurrentUrl { get; private set; } = default!;

    public NavigationManager(ILogger<NavigationManager> logger)
    {
        this.logger = logger;
    }

    public void NavigateTo(string url)
    {
        if (CurrentUrl is not null)
        {
            history.Push(CurrentUrl);
        }
        CurrentUrl = url;

        logger.LogInformation($"Navigated to: {CurrentUrl}");
    }

    public void GoBack()
    {
        logger.LogInformation($"Leaving: {CurrentUrl}");

        CurrentUrl = history.Count > 0 ? history.Pop() : null!;
    }
}