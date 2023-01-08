namespace BookScraper;

public static class Utils
{
    public static string GetPath(string uri)
    {
        return new Uri(uri).LocalPath[1..];
    }

    public static void CreateDirectory(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath!);

        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

}
