using System.Text.Json;

namespace VrcPresence;

/// <summary>Alle Einstellungen. Werden aus config.json (neben der .exe) geladen.</summary>
sealed class Settings
{
    // ---- Discord ----
    public string DiscordAppId { get; set; } = "1573741136206503947";
    /// <summary>Name eines im Developer Portal hochgeladenen Bildes (Standardbild). Leer = keins.</summary>
    public string LargeImageKey { get; set; } = "vrchat";

    // ---- VRChat-API ----
    public string ContactEmail { get; set; } = "";
    public bool AskForLogin { get; set; } = true;
    public double StatusPollMinutes { get; set; } = 2;
    public double UpdateIntervalSeconds { get; set; } = 1;

    // ---- Was angezeigt wird ----
    public bool ShowWorldImage { get; set; } = true;
    public bool ShowWorldButton { get; set; } = true;
    public string WorldButtonLabel { get; set; } = "View World";
    public bool ShowStatus { get; set; } = true;
    public bool ShowInstanceType { get; set; } = true;
    public bool ShowPlayerCount { get; set; } = true;
    public bool ShowTimer { get; set; } = true;

    // ---- Privatsphäre ----
    public bool HideWorldInPrivateInstances { get; set; } = true;
    public List<string> PrivateInstanceTypes { get; set; } =
        new() { "Friends+", "Friends", "Invite", "Invite+", "Group", "Group+" };
    public string PrivateInstanceText { get; set; } = "In a private instance";
    public bool HideOnDoNotDisturb { get; set; } = true;
    public string DoNotDisturbText { get; set; } = "Do Not Disturb";

    // ---- Texte ----
    public string LoadingText { get; set; } = "Loading...";
    public string Separator { get; set; } = " · ";
    /// <summary>{0} = aktuelle Spieler, {1} = maximale Spieler</summary>
    public string PlayerCountFormat { get; set; } = "{0}/{1}";
    /// <summary>Wird benutzt, wenn die maximale Spielerzahl unbekannt ist (z. B. ohne Login)</summary>
    public string PlayerCountFallbackFormat { get; set; } = "{0} players";
    /// <summary>VRChat-Status (API-Wert) -> angezeigter Text. Leerer Text = Status nicht anzeigen.</summary>
    public Dictionary<string, string> StatusLabels { get; set; } = new()
    {
        ["join me"] = "Join Me",
        ["active"] = "Online",
        ["ask me"] = "Ask Me",
        ["busy"] = "Do Not Disturb"
    };

    // ---------------------------------------------------------------

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "config.json");

    public string BuildUserAgent() =>
        string.IsNullOrWhiteSpace(ContactEmail)
            ? "VrcPresence/1.0 (private tool)"
            : $"VrcPresence/1.0 (private tool; contact: {ContactEmail})";

    public static Settings? Load(out string? error)
    {
        error = null;
        try
        {
            if (!File.Exists(FilePath))
            {
                var defaults = new Settings();
                File.WriteAllText(FilePath, JsonSerializer.Serialize(defaults, Json));
                return defaults;
            }
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }
}
