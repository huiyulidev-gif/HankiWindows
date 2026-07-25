namespace Hanki.Infrastructure.Logging;

public sealed class PrivacySafeLogger
{
    private readonly object _sync = new();

    public void Error(string location, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogDirectory);
            var line = $"{DateTimeOffset.UtcNow:O}\tERROR\t{Sanitize(location)}\t{exception.GetType().Name}{Environment.NewLine}";
            lock (_sync)
            {
                File.AppendAllText(
                    Path.Combine(AppPaths.LogDirectory, $"hanki-{DateTime.UtcNow:yyyyMMdd}.log"),
                    line);
            }
        }
        catch
        {
            // Logging must never interrupt typing or app startup.
        }
    }

    public void Info(string location)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogDirectory);
            var line = $"{DateTimeOffset.UtcNow:O}\tINFO\t{Sanitize(location)}{Environment.NewLine}";
            lock (_sync)
            {
                File.AppendAllText(
                    Path.Combine(AppPaths.LogDirectory, $"hanki-{DateTime.UtcNow:yyyyMMdd}.log"),
                    line);
            }
        }
        catch
        {
        }
    }

    private static string Sanitize(string value) =>
        new(value.Where(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-').ToArray());
}
