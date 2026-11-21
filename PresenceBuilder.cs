namespace VrcPresence;

/// <summary>Baut aus Zustand + Einstellungen die Discord-Presence. Hier änderst du, WAS angezeigt wird.</summary>
static class PresenceBuilder
{
    static bool _invalidPlayerCountFormatLogged;

    public static RichPresence Build(Settings cfg, WorldState w, string? vrcStatus, WorldInfo? info)
    {
        var rp = new RichPresence { Assets = new Assets() };
        if (!string.IsNullOrWhiteSpace(cfg.LargeImageKey))
        {
            rp.Assets.LargeImageKey = cfg.LargeImageKey;
            rp.Assets.LargeImageText = "VRChat";
        }

        // Do Not Disturb (API-Wert "busy"): keine Welt, kein Modus, keine Spielerzahl
        if (vrcStatus == "busy" && cfg.HideOnDoNotDisturb)
        {
            rp.Details = Fit(cfg.DoNotDisturbText);
            return rp;
        }

        if (w.WorldName == null)
        {
            rp.Details = Fit(cfg.LoadingText);
            return rp;
        }

        bool isPrivate = vrcStatus == "ask me"
            || (cfg.HideWorldInPrivateInstances
                && w.InstanceType != null
                && cfg.PrivateInstanceTypes.Contains(w.InstanceType, StringComparer.OrdinalIgnoreCase));

        if (isPrivate)
        {
            // Weder Weltname noch Bild noch Button
            rp.Details = Fit(cfg.PrivateInstanceText);
        }
        else
        {
            rp.Details = Fit(w.WorldName);

            if (cfg.ShowWorldImage && info?.ImageUrl != null)
            {
                rp.Assets.LargeImageKey = info.ImageUrl;
                rp.Assets.LargeImageText = Fit(w.WorldName);
            }

            if (cfg.ShowWorldButton && w.WorldId != null)
            {
                rp.Buttons = new[]
                {
                    new Button
                    {
                        Label = Fit(cfg.WorldButtonLabel, 32),
                        Url = $"https://vrchat.com/home/world/{w.WorldId}"
                    }
                };
            }
        }

        var parts = new List<string>();
        if (cfg.ShowStatus)
        {
            var label = StatusLabel(cfg, vrcStatus);
            if (!string.IsNullOrEmpty(label)) parts.Add(label);
        }
        if (cfg.ShowInstanceType && w.InstanceType != null) parts.Add(w.InstanceType);
        var players = info?.CurrentPlayers ?? (w.PlayersKnown ? w.Players : null);
        if (cfg.ShowPlayerCount && players is >= 0)
            parts.Add(PlayerText(cfg, players.Value, info?.Capacity));
        if (parts.Count > 0) rp.State = Fit(string.Join(cfg.Separator, parts));

        if (cfg.ShowTimer && w.PlayStartedAt != null)
            rp.Timestamps = new Timestamps(w.PlayStartedAt.Value.ToUniversalTime());

        return rp;
    }

    static string PlayerText(Settings cfg, int players, int? capacity)
    {
        try
        {
            return capacity is > 0
                ? string.Format(cfg.PlayerCountFormat, players, capacity)
                : string.Format(cfg.PlayerCountFallbackFormat, players);
        }
        catch (FormatException)
        {
            if (!_invalidPlayerCountFormatLogged)
            {
                Ui.LogWarning("Invalid player count format in config.json; using the built-in format.");
                _invalidPlayerCountFormatLogged = true;
            }
            return capacity is > 0 ? $"{players}/{capacity}" : $"{players}";
        }
    }

    static string? StatusLabel(Settings cfg, string? s) =>
        s != null && cfg.StatusLabels.TryGetValue(s, out var label) ? label : null;

    public static string StatusName(Settings cfg, string? s) =>
        StatusLabel(cfg, s) is { Length: > 0 } l ? l : "Unknown";

    static string Fit(string s, int max = 128) => s.Length > max ? s[..(max - 1)] + "…" : s;
}
