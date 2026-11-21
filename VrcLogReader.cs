namespace VrcPresence;

/// <summary>Liest die VRChat-Logdatei (nur lesend, ohne den Prozess anzufassen).</summary>
sealed class VrcLogReader
{
    static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "VRChat", "VRChat");

    static readonly Regex JoinRx = new(@"\[Behaviour\] Joining (wrld_[0-9a-fA-F\-]+):(\S+)", RegexOptions.Compiled);
    static readonly Regex NameRx = new(@"\[Behaviour\] (?:Entering Room|Joining or Creating Room): (.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex PlayerJoinedRx = new(@"\[Behaviour\]\s+OnPlayerJoined\b", RegexOptions.Compiled);
    static readonly Regex PlayerLeftRx = new(@"\[Behaviour\]\s+OnPlayerLeft\b", RegexOptions.Compiled);

    string? _file;
    long _pos;
    string _carry = "";
    bool _missingLogWarningShown;

    public WorldState State { get; } = new();

    public void Poll()
    {
        var latest = FindLatest();
        if (latest == null)
        {
            if (!_missingLogWarningShown)
            {
                Ui.LogWarning($"No VRChat log file was found in '{LogDir}'.");
                _missingLogWarningShown = true;
            }
            return;
        }
        _missingLogWarningShown = false;

        if (latest != _file)
        {
            _file = latest;
            _pos = 0;
            _carry = "";
            State.Reset();
        }

        using var fs = new FileStream(latest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (fs.Length < _pos)
        {
            _pos = 0;
            _carry = "";
            State.Reset();
        }
        if (fs.Length == _pos) return;

        fs.Seek(_pos, SeekOrigin.Begin);
        using var sr = new StreamReader(fs, Encoding.UTF8);
        var text = _carry + sr.ReadToEnd();
        _pos = fs.Position;

        var lines = text.Split('\n');
        _carry = lines[^1]; // unvollständige letzte Zeile aufheben
        for (int i = 0; i < lines.Length - 1; i++)
            Handle(lines[i].TrimEnd('\r'));
    }

    void Handle(string line)
    {
        var m = JoinRx.Match(line);
        if (m.Success)
        {
            State.ResetWorld();
            State.WorldId = m.Groups[1].Value;
            State.InstanceId = m.Groups[2].Value;
            State.InstanceType = InstanceTypeOf(m.Groups[2].Value);
            State.Since = ParseTime(line) ?? DateTime.Now;
            State.PlayStartedAt ??= State.Since;
            return;
        }

        m = NameRx.Match(line);
        if (m.Success)
        {
            State.WorldName = m.Groups[1].Value;
            return;
        }

        if (PlayerJoinedRx.IsMatch(line))
        {
            State.Players++;
            State.PlayersKnown = true;
        }
        else if (PlayerLeftRx.IsMatch(line))
        {
            State.Players = Math.Max(0, State.Players - 1);
            State.PlayersKnown = true;
        }
    }

    static string InstanceTypeOf(string instance)
    {
        if (instance.Contains("~group("))
        {
            if (instance.Contains("~groupAccessType(public)")) return "Group Public";
            if (instance.Contains("~groupAccessType(plus)")) return "Group+";
            return "Group";
        }
        if (instance.Contains("~hidden(")) return "Friends+";
        if (instance.Contains("~friends(")) return "Friends";
        if (instance.Contains("~private("))
            return instance.Contains("~canRequestInvite") ? "Invite+" : "Invite";
        return "Public";
    }

    static DateTime? ParseTime(string line)
    {
        if (line.Length >= 19 &&
            DateTime.TryParseExact(line[..19], "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var t))
            return t;
        return null;
    }

    static string? FindLatest()
    {
        if (!Directory.Exists(LogDir)) return null;
        return new DirectoryInfo(LogDir).GetFiles("output_log_*.txt")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }
}
