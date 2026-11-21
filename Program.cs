namespace VrcPresence;

static class Program
{
    static async Task Main()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception ex)
        {
            Ui.LogError("Fatal error; the application is shutting down.", ex);
            Console.Error.WriteLine($"FATAL ERROR: {ex.Message}");
            WaitForKey();
        }
        finally
        {
            try
            {
                Console.CursorVisible = true;
            }
            catch (IOException ex)
            {
                AppLog.Warning("Could not restore console cursor visibility.", ex);
            }
            catch (InvalidOperationException ex)
            {
                AppLog.Warning("Could not restore console cursor visibility.", ex);
            }
        }
    }

    static async Task RunAsync()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("VRChat Rich Presence - Press Ctrl+C to exit");

        var cfg = Settings.Load(out var cfgError);
        if (cfg == null)
        {
            Ui.LogError($"Failed to load config.json: {cfgError}");
            Console.WriteLine($"ERROR: config.json is invalid: {cfgError}");
            Console.WriteLine($"File: {Settings.FilePath}");
            WaitForKey();
            return;
        }
        if (string.IsNullOrWhiteSpace(cfg.DiscordAppId) || cfg.DiscordAppId == "DEINE_APPLICATION_ID")
        {
            Ui.LogError("The Discord Application ID (discordAppId) is missing from config.json.");
            Console.WriteLine("ERROR: The Discord Application ID (discordAppId) is missing from config.json.");
            Console.WriteLine($"File: {Settings.FilePath}");
            WaitForKey();
            return;
        }

        using var api = new VrcApi(cfg);
        if (!await api.TryRestoreAsync() && cfg.AskForLogin)
        {
            Console.Write("Log in to VRChat for status, world image, and max player count? (y/N): ");
            if (Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                Console.Write("Email address or username: ");
                var user = Console.ReadLine()?.Trim() ?? "";
                var pass = ReadSecret("Password: ");
                if (await api.LoginAsync(user, pass)) Console.WriteLine("Logged in.");
            }
        }

        using var discord = new DiscordRpcClient(cfg.DiscordAppId);
        discord.OnReady += (_, e) =>
        {
            Ui.DiscordState = $"Connected as {e.User.Username}";
            Ui.Log("Discord connected.");
        };
        discord.OnConnectionFailed += (_, _) =>
        {
            Ui.DiscordState = "Not connected - is the Discord desktop app running?";
            Ui.LogError("Could not connect to Discord. Check that the Discord desktop app is running.");
        };
        discord.OnError += (_, e) => Ui.LogError($"Discord error: {e.Message}");
        discord.Initialize();

        var log = new VrcLogReader();
        string? lastKey = null;
        bool active = false;
        WorldInfo? info = null;
        string? infoFor = null;
        Exception? lastLoopError = null;
        bool needsApiInfo = cfg.ShowWorldImage || cfg.ShowPlayerCount;
        int delayMs = (int)(Math.Max(0.5, cfg.UpdateIntervalSeconds) * 1000);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        Console.Clear();
        Console.CursorVisible = false;
        Ui.Log(api.LoggedIn ? "Started with a VRChat login." : "Started without a VRChat login.");
        Ui.Log($"Log file: {AppLog.FilePath}");

        while (!cts.IsCancellationRequested)
        {
            try
            {
                Ui.ApiState = api.LoggedIn
                    ? PresenceBuilder.StatusName(cfg, api.Status)
                    : "Not logged in (no status)";

                if (!VrcRunning())
                {
                    Ui.VrcState = "Not running";
                    if (active)
                    {
                        discord.ClearPresence();
                        active = false;
                        lastKey = null;
                        Ui.Current = null;
                        Ui.Since = null;
                        Ui.ImageInfo = "None";
                        Ui.Log("VRChat stopped; presence cleared.");
                    }
                }
                else
                {
                    if (Ui.VrcState != "Running")
                    {
                        Ui.VrcState = "Running";
                        Ui.Log("VRChat detected.");
                    }

                    log.Poll();
                    await api.RefreshAsync();

                    var worldId = log.State.WorldId;
                    if (worldId == null)
                    {
                        info = null;
                        infoFor = null;
                    }
                    else if (needsApiInfo && api.LoggedIn)
                    {
                        var infoKey = $"{worldId}:{log.State.InstanceId}";
                        if (infoKey != infoFor)
                        {
                            infoFor = infoKey;
                            info = await api.GetWorldInfoAsync(worldId, log.State.InstanceId ?? "");
                            Ui.Log($"World info: image {(info.ImageUrl != null ? "available" : "unavailable")}, max players {info.Capacity?.ToString() ?? "unknown"}.");
                        }
                    }

                    var rp = PresenceBuilder.Build(cfg, log.State, api.Status, info);
                    Ui.ImageInfo = rp.Assets?.LargeImageKey ?? "None";

                    var key = $"{rp.Details}|{rp.State}|{log.State.Since:O}|{rp.Assets?.LargeImageKey}|{rp.Buttons?.Length}";
                    if (key != lastKey)
                    {
                        discord.SetPresence(rp);
                        lastKey = key;
                        active = true;
                        Ui.Log($"Presence updated: {rp.Details}");
                    }

                    Ui.Current = rp;
                    Ui.Since = log.State.PlayStartedAt;
                }

                lastLoopError = null;
            }
            catch (Exception ex)
            {
                if (lastLoopError == null ||
                    lastLoopError.GetType() != ex.GetType() ||
                    lastLoopError.Message != ex.Message)
                {
                    Ui.LogError("An error occurred during the main loop.", ex);
                }
                lastLoopError = ex;
            }

            Ui.Render();

            try
            {
                await Task.Delay(delayMs, cts.Token);
            }
            catch (TaskCanceledException) when (cts.IsCancellationRequested)
            {
            }
        }

        discord.ClearPresence();
    }

    static bool VrcRunning()
    {
        var procs = Process.GetProcessesByName("VRChat");
        try { return procs.Length > 0; }
        finally { foreach (var p in procs) p.Dispose(); }
    }

    static void WaitForKey()
    {
        if (Console.IsInputRedirected) return;
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey(true);
    }

    static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        var sb = new StringBuilder();
        ConsoleKeyInfo k;
        while ((k = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            if (k.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0) { sb.Length--; Console.Write("\b \b"); }
            }
            else if (!char.IsControl(k.KeyChar))
            {
                sb.Append(k.KeyChar);
                Console.Write('*');
            }
        }
        Console.WriteLine();
        return sb.ToString();
    }
}
