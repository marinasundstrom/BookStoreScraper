namespace BookScraper.Tests;

public class UnitTest1
{
    string baseUrl = "http://books.toscrape.com";
    string currentUrl = "http://books.toscrape.com/test";

    [Fact]
    public void Test1()
    {
        var newurl = UrlHelpers.AsAbsoluteUrl("http://books.toscrape.com", "http://books.toscrape.com/test/index.html", "../index.html");
    }

    [Fact]
    public void Test2()
    {
        var newurl = UrlHelpers.AsAbsoluteUrl("http://books.toscrape.com", "http://books.toscrape.com/test/index.html", "index.html");
    }

    [Fact]
    public void Test3()
    {
        var newurl = UrlHelpers.AsAbsoluteUrl("http://books.toscrape.com", "http://books.toscrape.com/test/test2/index.html", "index.html");
    }

    [Fact]
    public void Test4()
    {
        var newurl = UrlHelpers.AsAbsoluteUrl("http://books.toscrape.com", "http://books.toscrape.com/test/test2/index.html", "../index.html");
    }  
}
