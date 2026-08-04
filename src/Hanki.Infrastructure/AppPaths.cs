namespace Hanki.Infrastructure;

public static class AppPaths
{
    public const string DataDirectoryEnvironmentVariable = "HANKI_DATA_DIRECTORY";

    /// <summary>
    /// Tests and local diagnostics may point Hanki at an isolated absolute directory. Normal
    /// launches do not set this variable and continue using the established per-user path.
    /// </summary>
    public static string DataDirectory
    {
        get
        {
            if (TryGetDataDirectoryOverride(out var configured))
                return configured;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Yulbyte",
                "Hanki");
        }
    }

    public static bool IsDataDirectoryOverridden => TryGetDataDirectoryOverride(out _);

    public static string DatabasePath => Path.Combine(DataDirectory, "hanki.db");
    public static string LogDirectory => Path.Combine(DataDirectory, "Logs");

    /// <summary>DPAPI-encrypted Supabase auth session. Kept separate from the shortcuts database and JSON settings.</summary>
    public static string AuthSessionPath => Path.Combine(DataDirectory, "auth.session");

    private static bool TryGetDataDirectoryOverride(out string path)
    {
        var configured = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured))
        {
            try
            {
                path = Path.GetFullPath(configured);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        path = string.Empty;
        return false;
    }
}
