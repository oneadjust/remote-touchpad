namespace RemoteTouchpad;

public static class AppLogger
{
    private static readonly object Lock = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteTouchpad",
        "logs");

    public static string LogPath { get; } = Path.Combine(LogDirectory, "remote-touchpad.log");

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception is null ? message : $"{message} | {exception}");
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}";

            lock (Lock)
            {
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // Logging must never break the app.
        }
    }
}
