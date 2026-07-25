namespace Hanki.Infrastructure;

public static class AppPaths
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yulbyte", "Hanki");

    public static string DatabasePath => Path.Combine(DataDirectory, "hanki.db");
    public static string LogDirectory => Path.Combine(DataDirectory, "Logs");
}
