namespace AIAgentHub.Infrastructure.Executors;

public static class StreamReaderExtensions
{
    public static async Task StreamChunksAsync(
        this StreamReader reader,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken,
        int bufferSize = 1024)
    {
        var buffer = new char[bufferSize];
        int read;
        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   (read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                var chunk = new string(buffer, 0, read);
                await onChunk(chunk).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }
}
