namespace VrcPresence;

static class Ui
{
    static readonly object Gate = new();
    static readonly List<string> Messages = new();

    public static string DiscordState = "Connecting...";
    public static string VrcState = "Not running";
    public static string ApiState = "Not logged in (no status)";
    public static string ImageInfo = "None";
    public static RichPresence? Current;
    public static DateTime? Since;
    static bool _renderWarningShown;

    public static void Log(string msg)
    {
        AppLog.Info(msg);
        AddMessage(msg);
    }

    public static void LogWarning(string msg, Exception? exception = null)
    {
        AppLog.Warning(msg, exception);
        AddMessage($"WARNING: {msg}");
    }

    public static void LogError(string msg, Exception? exception = null)
    {
        AppLog.Error(msg, exception);
        AddMessage($"ERROR: {msg}{(exception == null ? "" : $": {exception.Message}")}");
    }

    static void AddMessage(string msg)
    {
        lock (Gate)
        {
            Messages.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            if (Messages.Count > 6) Messages.RemoveAt(0);
        }
    }

    static string Elapsed(TimeSpan t) =>
        $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

    /// <summary>Zeichnet die Anzeige an der gleichen Stelle neu (kein Flackern, kein Scrollen).</summary>
    public static void Render()
    {
        try
        {
            int width = Math.Max(20, Console.WindowWidth - 1);
            int height = Math.Max(5, Console.WindowHeight - 1);

            var lines = new List<string>
            {
                "VRChat Rich Presence - Press Ctrl+C to exit",
                new string('-', Math.Min(width, 60)),
                $"Discord:        {DiscordState}",
                $"VRChat:         {VrcState}",
                $"VRChat status:  {ApiState}",
                "",
                "Current Discord presence:",
            };

            var p = Current;
            if (p == null)
            {
                lines.Add("  (no presence active)");
                for (int i = 0; i < 5; i++) lines.Add("");
            }
            else
            {
                lines.Add("  VRChat");
                lines.Add("  " + (p.Details ?? ""));
                lines.Add("  " + (p.State ?? ""));
                lines.Add(Since is { } since && p.Timestamps != null
                    ? $"  {Elapsed(DateTime.Now - since)} elapsed"
                    : "");
                lines.Add("  Image:  " + ImageInfo);
                lines.Add(p.Buttons is { Length: > 0 } b
                    ? $"  Button: {b[0].Label}"
                    : "  Button: none");
            }

            lines.Add("");
            lines.Add("Recent log messages:");
            lock (Gate)
            {
                for (int i = 0; i < 6; i++)
                    lines.Add(i < Messages.Count ? "  " + Messages[i] : "");
            }

            Console.SetCursorPosition(0, 0);
            foreach (var l in lines.Take(height))
            {
                var t = l.Length > width ? l[..width] : l;
                Console.WriteLine(t.PadRight(width));
            }
            _renderWarningShown = false;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            if (!_renderWarningShown)
            {
                AppLog.Warning("Could not render the console UI; it will be retried.", ex);
                _renderWarningShown = true;
            }
        }
    }
}
