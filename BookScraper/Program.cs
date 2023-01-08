using BookScraper;
using BookScraper.Processors;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder();

builder.Services.AddHttpClient();

builder.Services.AddScoped<MyHttpClient>();

builder.Services.AddScoped<ErrorSink>();

builder.Services.AddScoped<Context>();
builder.Services.AddScoped<NavigationManager>();

builder.Services.AddScoped<DocumentProcessor>();
builder.Services.AddScoped<ImgProcessor>();
builder.Services.AddScoped<LinkProcessor>();
builder.Services.AddScoped<ScriptProcessor>();

builder.Services.AddHostedService<ScraperHostedService>();
builder.Services.AddScoped<Scraper>();

var app = builder.Build();

await app.RunAsync();