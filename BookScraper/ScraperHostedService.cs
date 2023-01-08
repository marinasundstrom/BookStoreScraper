internal sealed class ScraperHostedService : IHostedService
{
    private readonly Scraper scraper;

    public ScraperHostedService(Scraper scraper)
    {
        this.scraper = scraper;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await scraper.Scrape(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
