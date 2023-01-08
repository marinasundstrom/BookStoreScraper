namespace BookScraper.Tests;

public class UrlHelpersTest
{
    [Fact]
    public void Test1()
    {
        var newurl = UrlHelpers.AsAbsoluteUrl("http://books.toscrape.com", "http://books.toscrape.com/test/index.html", "../index.html");

        Assert.Equal("http://books.toscrape.com/index.html", newurl);

    }

    [Fact]
    public void Test2()
    {
        var newurl = UrlHelpers.AsAbsoluteUrl("http://books.toscrape.com", "http://books.toscrape.com/test/index.html", "index.html");

        Assert.Equal("http://books.toscrape.com/test/index.html", newurl);

    }

    [Fact]
    public void Test3()
    {
        var newurl = UrlHelpers.AsAbsoluteUrl("http://books.toscrape.com", "http://books.toscrape.com/test/test2/index.html", "index.html");

        Assert.Equal("http://books.toscrape.com/test/test2/index.html", newurl);
    }

    [Fact]
    public void Test4()
    {
        var newurl = UrlHelpers.AsAbsoluteUrl("http://books.toscrape.com", "http://books.toscrape.com/test/test2/index.html", "../index.html");

        Assert.Equal("http://books.toscrape.com/test/index.html", newurl);

    }
}
