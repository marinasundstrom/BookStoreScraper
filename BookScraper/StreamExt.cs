public static class StreamExt
{
    // Modified version of StackOverflow: https://stackoverflow.com/questions/411592/how-do-i-save-a-stream-to-a-file-in-c
    public static async Task WriteToFileAsync(this Stream input, string file, CancellationToken cancellationToken = default)
    {
        using var stream = File.Create(file);

        await input.CopyToAsync(stream, cancellationToken);
    }
}
