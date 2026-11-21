namespace VrcPresence;

/// <summary>
/// Optional: Login, eigener Status (join me / active / ask me / busy), Welt-Bild und maximale Spielerzahl
/// über die inoffizielle VRChat-API. Die Session wird per Windows-DPAPI verschlüsselt gespeichert.
/// </summary>
sealed class VrcApi : IDisposable
{
    const string Base = "https://api.vrchat.cloud/api/1";
    static readonly Uri ApiUri = new("https://api.vrchat.cloud");
    static readonly string SessionFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VrcPresence", "session.bin");

    readonly CookieContainer _cookies = new();
    readonly HttpClient _http;
    readonly TimeSpan _pollInterval;
    readonly Dictionary<string, WorldInfo> _infoCache = new();
    DateTime _lastPoll = DateTime.MinValue;

    public string? Status { get; private set; }
    public bool LoggedIn { get; private set; }

    public VrcApi(Settings cfg)
    {
        _pollInterval = TimeSpan.FromMinutes(Math.Max(0.5, cfg.StatusPollMinutes));
        var handler = new HttpClientHandler { CookieContainer = _cookies };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", cfg.BuildUserAgent());
    }

    public async Task<bool> TryRestoreAsync()
    {
        if (!File.Exists(SessionFile)) return false;
        try
        {
            var json = Encoding.UTF8.GetString(
                ProtectedData.Unprotect(File.ReadAllBytes(SessionFile), null, DataProtectionScope.CurrentUser));
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (d == null)
            {
                Ui.LogWarning("The saved VRChat session file is empty or invalid.");
                return false;
            }
            foreach (var (k, v) in d) _cookies.Add(ApiUri, new Cookie(k, v));
            var restored = await FetchStatusAsync();
            if (!restored)
                Ui.LogWarning("The saved VRChat session could not be restored; log in again if needed.");
            return restored;
        }
        catch (Exception ex)
        {
            Ui.LogWarning("Could not restore the saved VRChat session; log in again if needed.", ex);
            return false;
        }
    }

    public async Task<bool> LoginAsync(string user, string pass)
    {
        var basic = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(pass)}"));

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{Base}/auth/user");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        using var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            var message = $"VRChat login failed ({(int)res.StatusCode}): {ApiError(body)}";
            Ui.LogError(message);
            Console.WriteLine($"ERROR: {message}");
            return false;
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("requiresTwoFactorAuth", out var tf))
        {
            var methods = tf.EnumerateArray().Select(x => x.GetString()).ToList();
            var path = methods.Contains("totp") ? "totp" : methods.Contains("emailOtp") ? "emailotp" : "otp";

            Console.Write($"Two-factor authentication code ({path}): ");
            var code = Console.ReadLine()?.Trim() ?? "";
            var content = new StringContent(JsonSerializer.Serialize(new { code }), Encoding.UTF8, "application/json");
            using var v = await _http.PostAsync($"{Base}/auth/twofactorauth/{path}/verify", content);
            if (!v.IsSuccessStatusCode)
            {
                var message = $"Two-factor authentication failed ({(int)v.StatusCode}): {ApiError(await v.Content.ReadAsStringAsync())}";
                Ui.LogError(message);
                Console.WriteLine($"ERROR: {message}");
                return false;
            }
        }

        SaveSession();
        var loggedIn = await FetchStatusAsync();
        if (!loggedIn)
            Ui.LogError("VRChat login completed, but the account status could not be retrieved.");
        return loggedIn;
    }

    public async Task RefreshAsync()
    {
        if (!LoggedIn || DateTime.UtcNow - _lastPoll < _pollInterval) return;
        _lastPoll = DateTime.UtcNow;
        if (!await FetchStatusAsync() && !LoggedIn)
            Ui.LogError("The VRChat session expired. Log in again to restore account status.");
    }

    /// <summary>Welt-Bild und maximale Spielerzahl (pro Instanz nur einmal abgefragt, danach aus dem Cache).</summary>
    public async Task<WorldInfo> GetWorldInfoAsync(string worldId, string instanceId)
    {
        var key = $"{worldId}:{instanceId}";
        if (_infoCache.TryGetValue(key, out var cached)) return cached;

        string? image = null;
        int? capacity = null;
        bool cacheable = false;
        try
        {
            using (var res = await _http.GetAsync($"{Base}/worlds/{worldId}"))
            {
                cacheable = res.IsSuccessStatusCode || res.StatusCode == HttpStatusCode.NotFound;
                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                    var root = doc.RootElement;
                    foreach (var name in new[] { "thumbnailImageUrl", "imageUrl" })
                    {
                        if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String &&
                            p.GetString() is { Length: > 0 and <= 256 } u)
                        {
                            image = u;
                            break;
                        }
                    }
                    capacity = ReadInt(root, "capacity"); // Fallback: Kapazität der Welt
                }
                else if (res.StatusCode != HttpStatusCode.NotFound)
                {
                    Ui.LogWarning($"VRChat world information request failed ({(int)res.StatusCode} {res.ReasonPhrase}).");
                }
            }

            // Genauere Kapazität der konkreten Instanz (falls abrufbar)
            if (!string.IsNullOrEmpty(instanceId))
            {
                using var ires = await _http.GetAsync($"{Base}/instances/{worldId}:{Uri.EscapeDataString(instanceId)}");
                if (ires.IsSuccessStatusCode)
                {
                    using var idoc = JsonDocument.Parse(await ires.Content.ReadAsStringAsync());
                    var c = ReadInt(idoc.RootElement, "capacity");
                    if (c is > 0) capacity = c;
                }
                else if (ires.StatusCode != HttpStatusCode.NotFound)
                {
                    Ui.LogWarning($"VRChat instance information request failed ({(int)ires.StatusCode} {ires.ReasonPhrase}).");
                }
            }
        }
        catch (Exception ex)
        {
            Ui.LogWarning("Could not retrieve VRChat world or instance information.", ex);
        }

        var info = new WorldInfo(image, capacity);
        if (cacheable) _infoCache[key] = info;
        return info;
    }

    async Task<bool> FetchStatusAsync()
    {
        using var res = await _http.GetAsync($"{Base}/auth/user");
        if (res.StatusCode == HttpStatusCode.Unauthorized)
        {
            LoggedIn = false;
            Status = null;
            return false;
        }
        if (!res.IsSuccessStatusCode)
        {
            Ui.LogWarning($"VRChat status request failed ({(int)res.StatusCode} {res.ReasonPhrase}).");
            return false;
        }

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        if (doc.RootElement.TryGetProperty("requiresTwoFactorAuth", out _))
        {
            LoggedIn = false;
            Status = null;
            Ui.LogWarning("VRChat requires two-factor authentication to restore this session.");
            return false;
        }

        Status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
        LoggedIn = true;
        return true;
    }

    void SaveSession()
    {
        var d = _cookies.GetCookies(ApiUri).Cast<Cookie>().ToDictionary(c => c.Name, c => c.Value);
        Directory.CreateDirectory(Path.GetDirectoryName(SessionFile)!);
        var enc = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(d)), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(SessionFile, enc);
    }

    static int? ReadInt(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v)
            ? (int?)v
            : null;

    static string ApiError(string body)
    {
        try
        {
            using var d = JsonDocument.Parse(body);
            if (d.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m))
                return m.GetString() ?? body;
        }
        catch { }
        return body.Length > 200 ? body[..200] : body;
    }

    public void Dispose() => _http.Dispose();
}
