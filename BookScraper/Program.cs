using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder();

builder.Services.AddHostedService<ScraperHostedService>();
builder.Services.AddTransient<Scraper>();

var app = builder.Build();

await app.RunAsync();