using System;

namespace BookScraper;

public sealed class ErrorSink
{
    private readonly HashSet<string> errors = new HashSet<string>();

    public bool HasErrors => errors.Count > 0;

    public IReadOnlyCollection<string> Errors => errors;

    public void AddFile(string filePath)
    {
        errors.Add(filePath);
    }
}

