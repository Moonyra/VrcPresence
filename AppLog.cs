namespace VrcPresence;

static class AppLog
{
    static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VrcPresence", "VrcPresence.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warning(string message, Exception? exception = null) =>
        Write("WARNING", message, exception);
    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", message, exception);

    static void Write(string level, string message, Exception? exception = null)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        if (exception != null) entry += Environment.NewLine + exception;

        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, entry + Environment.NewLine);
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"WARNING: Could not write to the log file '{FilePath}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine($"WARNING: Could not write to the log file '{FilePath}': {ex.Message}");
            }
        }
    }
}
