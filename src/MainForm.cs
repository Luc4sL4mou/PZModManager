using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using PZModManager.Core;

namespace PZModManager;

public sealed class MainForm : Form
{
    readonly WebView2 _web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(20, 22, 24) };
    readonly SteamWorkshop _ws = new();
    GamePaths _paths = GamePaths.Detect();
    readonly List<FileSystemWatcher> _watchers = new();
    readonly System.Windows.Forms.Timer _debounce = new() { Interval = 1500 };
    readonly System.Windows.Forms.Timer _poll = new() { Interval = 5000 };
    bool _lastGameRunning;

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public MainForm()
    {
        Text = "PZ Mod Manager";
        Width = 1280; Height = 820;
        MinimumSize = new Size(980, 640);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized; // ao restaurar, volta para 1280x820 centralizado
        BackColor = Color.FromArgb(20, 22, 24);
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
            if (s != null) Icon = new Icon(s);
        Controls.Add(_web);
        _debounce.Tick += (_, _) => { _debounce.Stop(); Push("changed"); };
        _poll.Tick += async (_, _) =>
        {
            if (_polling) return;
            _polling = true;
            try
            {
                var running = await Task.Run(GamePaths.IsGameRunning);
                if (running != _lastGameRunning) { _lastGameRunning = running; Push("changed"); }
            }
            catch (Exception ex) { Log.Error("poll", ex); }
            finally { _polling = false; }
        };
        Load += async (_, _) =>
        {
            try { await InitWeb(); }
            catch (Exception ex)
            {
                Log.Error("InitWeb", ex);
                MessageBox.Show(this, Lang.T("Não foi possível iniciar a interface (WebView2).", "Couldn't start the interface (WebView2).") + "\n\n" + ex.Message,
                    "PZ Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        StartUiWatchdog();
    }

    bool _polling;
    volatile string _currentCmd = "";
    long _uiTick = Environment.TickCount64;

    /// <summary>Registra no log se a thread da janela ficar travada (e qual comando estava rodando).</summary>
    void StartUiWatchdog()
    {
        var t = new Thread(() =>
        {
            bool reported = false;
            while (true)
            {
                Thread.Sleep(2000);
                if (IsDisposed) return;
                try { if (IsHandleCreated) BeginInvoke(() => Interlocked.Exchange(ref _uiTick, Environment.TickCount64)); }
                catch { return; }
                var lag = Environment.TickCount64 - Interlocked.Read(ref _uiTick);
                if (lag > 6000 && !reported)
                {
                    Log.Write($"AVISO: interface sem responder há {lag / 1000}s. Comando em andamento: '{_currentCmd}'");
                    reported = true;
                }
                else if (lag < 3000 && reported) { Log.Write("Interface voltou a responder."); reported = false; }
            }
        }) { IsBackground = true, Name = "ui-watchdog" };
        t.Start();
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // barra de título escura (tema fixo escuro)
        int on = 1;
        if (DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref on, sizeof(int));
        int caption = 0x00181614; // COLORREF (BGR) da barra: #141618
        DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
    }

    async Task InitWeb()
    {
        var udf = Path.Combine(GamePaths.DataDir, "webview");
        var env = await CoreWebView2Environment.CreateAsync(null, udf);
        await _web.EnsureCoreWebView2Async(env);
        var cw = _web.CoreWebView2;
        cw.Settings.AreDevToolsEnabled = Debugger.IsAttached;
        cw.Settings.IsStatusBarEnabled = false;
        cw.Settings.AreDefaultContextMenusEnabled = Debugger.IsAttached;
        cw.Settings.IsZoomControlEnabled = false;
        // links externos abrem no navegador padrão
        cw.NewWindowRequested += (_, e) => { e.Handled = true; OpenExternal(e.Uri); };
        cw.WebMessageReceived += async (_, e) =>
        {
            // só a nossa página fala com a ponte
            if (!e.Source.StartsWith("https://pzmm.local/", StringComparison.OrdinalIgnoreCase)) return;
            await OnMessage(e.TryGetWebMessageAsString());
        };

        cw.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith("https://pzmm.local/", StringComparison.OrdinalIgnoreCase))
            { e.Cancel = true; OpenExternal(e.Uri); }
        };

        string html;
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ui.index.html")!)
        using (var r = new StreamReader(s)) html = r.ReadToEnd();
        var uiDir = Path.Combine(GamePaths.DataDir, "ui");
        Directory.CreateDirectory(uiDir);
        File.WriteAllText(Path.Combine(uiDir, "index.html"), html);
        cw.SetVirtualHostNameToFolderMapping("pzmm.local", uiDir, CoreWebView2HostResourceAccessKind.DenyCors);
        cw.Navigate("https://pzmm.local/index.html");
        SetupWatchers();
        _lastGameRunning = GamePaths.IsGameRunning();
        _poll.Start();
    }

    void SetupWatchers()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        void Watch(string? dir, string filter, bool subdirs)
        {
            if (dir == null || !Directory.Exists(dir)) return;
            var w = new FileSystemWatcher(dir, filter)
            {
                IncludeSubdirectories = subdirs,
                NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite,
            };
            void Kick()
            {
                try { if (IsHandleCreated && !IsDisposed) BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); }); }
                catch { }
            }
            w.Created += (_, _) => Kick(); w.Deleted += (_, _) => Kick(); w.Changed += (_, _) => Kick();
            w.Renamed += (_, _) => Kick();
            w.Error += (_, e) => Log.Error("watcher " + dir, e.GetException());
            w.EnableRaisingEvents = true;
            _watchers.Add(w);
        }
        Watch(_paths.WorkshopContent, "*", false);
        Watch(_paths.WorkshopAcf != null ? Path.GetDirectoryName(_paths.WorkshopAcf) : null, "appworkshop_108600.acf", false);
        Watch(Path.Combine(_paths.ZomboidPath, "mods"), "*", false);
        Watch(Path.Combine(_paths.ZomboidPath, "Server"), "*.ini", false);
        Watch(_paths.GamePath, "ProjectZomboid64.*", false);
    }

    void Push(string evt, object? data = null)
    {
        try
        {
            if (_web.CoreWebView2 == null) return;
            var msg = JsonSerializer.Serialize(new { @event = evt, data }, Json);
            _web.CoreWebView2.PostWebMessageAsString(msg);
        }
        catch (Exception ex) { Log.Error("push", ex); }
    }

    async Task OnMessage(string raw)
    {
        JsonNode? msg;
        try { msg = JsonNode.Parse(raw); } catch { return; }
        var id = msg?["id"]?.GetValue<int>() ?? 0;
        var cmd = msg?["cmd"]?.GetValue<string>() ?? "";
        var args = msg?["args"] as JsonObject ?? new JsonObject();
        object? result = null; string? error = null;
        var sw = Stopwatch.StartNew();
        bool quiet = cmd is "getState" or "getProfile" or "open" or "steamStatus" or "setLang";
        if (!quiet) Log.Write($"> {cmd}");
        _currentCmd = cmd;
        try
        {
            // tudo que mexe em disco/rede roda fora da thread da janela; só "confirm" precisa dela
            result = cmd == "confirm"
                ? await HandleCommand(cmd, args)
                : await Task.Run(() => HandleCommand(cmd, args));
        }
        catch (Exception ex) { error = ex.Message; Log.Error(cmd, ex); }
        finally { _currentCmd = ""; }
        if (!quiet || sw.ElapsedMilliseconds > 3000) Log.Write($"< {cmd} {(error == null ? "ok" : "falhou")} em {sw.ElapsedMilliseconds} ms");
        try
        {
            var reply = JsonSerializer.Serialize(new { id, ok = error == null, data = result, error }, Json);
            _web.CoreWebView2?.PostWebMessageAsString(reply);
        }
        catch (Exception ex) { Log.Error("reply " + cmd, ex); }
    }

    static string S(JsonObject a, string k) => a[k]?.GetValue<string>() ?? "";
    static bool B(JsonObject a, string k) => a[k]?.GetValue<bool>() ?? false;
    static List<string> L(JsonObject a, string k) =>
        (a[k] as JsonArray)?.Select(x => x?.GetValue<string>() ?? "").Where(x => x != "").ToList() ?? new();

    async Task<object?> HandleCommand(string cmd, JsonObject a)
    {
        switch (cmd)
        {
            case "getState":
                if (B(a, "redetect"))
                {
                    _paths = GamePaths.Detect();
                    BeginInvoke(SetupWatchers);
                }
                return BuildState();

            case "fetchRemote":
            {
                // detalhes + itens obrigatórios dos itens instalados (em segundo plano)
                var force = B(a, "force");
                var scan = await Task.Run(() => ModScanner.Scan(_paths));
                var ids = scan.Items.Select(i => i.Id).ToList();
                await _ws.FetchDetails(ids, force);
                await _ws.FetchRequired(ids, force);
                // detalhes dos itens obrigatórios ainda não instalados (para mostrar título)
                var req = _ws.CachedRequired().Where(kv => ids.Contains(kv.Key)).SelectMany(kv => kv.Value.Select(r => r.Id));
                await _ws.FetchDetails(req, false);
                return true;
            }

            case "getRecommended":
            {
                var force = B(a, "force");
                var addons = await _ws.FetchViewpointAddons(force);
                var all = Recommendations.Curated.Select(c => c.Id).Concat(addons).Distinct().ToList();
                await _ws.FetchDetails(all, force);
                var cache = _ws.CachedItems();
                return new
                {
                    curated = Recommendations.Curated,
                    addons,
                    items = all.Where(cache.ContainsKey).Select(i => cache[i]).ToList(),
                };
            }

            case "getProfile":
            {
                var r = Profiles.Find(_paths, S(a, "key")) ?? throw new Exception("Perfil não encontrado.");
                return Profiles.Read(r);
            }

            case "saveProfile":
            {
                var r = Profiles.Find(_paths, S(a, "key")) ?? throw new Exception("Perfil não encontrado.");
                var active = L(a, "active");
                List<string>? wsItems = r.Type == "server" ? L(a, "workshopItems") : null;
                List<string>? maps = a["maps"] is JsonArray ? L(a, "maps") : null;
                Profiles.Write(_paths, r, active, wsItems, maps);
                return true;
            }

            case "steamStatus":
            {
                // inscrições feitas a partir deste momento (unix, segundos) contam
                var since = a["since"]?.GetValue<double>() is double d && d > 0
                    ? DateTimeOffset.FromUnixTimeSeconds((long)d).LocalDateTime
                    : DateTime.Now.AddHours(-1);
                return SteamStatus.Get(_paths, since);
            }

            case "zbInstall":
                return ZombieBuddy.Install(_paths);

            case "zbUninstall":
                return ZombieBuddy.Uninstall(_paths);

            case "setLang":
                Lang.Set(S(a, "lang"));
                return true;

            case "restoreLauncher":
                return Launcher.RestoreOriginals(_paths);

            case "setMemory":
                return GameMemory.Set(_paths, a["mb"]?.GetValue<int>() ?? 0);

            case "getConflicts":
            {
                // só os mods ativos (a varredura de scripts/mapas é pesada para rodar a cada getState)
                var keys = L(a, "keys").ToHashSet(StringComparer.OrdinalIgnoreCase);
                var mods = ModScanner.Scan(_paths).Mods.Where(m => keys.Contains(m.Key));
                return Conflicts.Scan(mods);
            }

            case "open":
                OpenExternal(S(a, "url"));
                return true;

            case "openFolder":
            {
                var path = S(a, "path");
                var full = Path.GetFullPath(path);
                var allowed = new[] { _paths.GamePath, _paths.WorkshopContent, _paths.ZomboidPath, GamePaths.DataDir }
                    .Where(x => x != null).Select(x => Path.TrimEndingDirectorySeparator(Path.GetFullPath(x!)));
                // compara com a barra no fim: "ProjectZomboidX" não pode passar por "ProjectZomboid"
                if (!allowed.Any(x => full.Equals(x, StringComparison.OrdinalIgnoreCase) ||
                                      full.StartsWith(x + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    throw new Exception("Pasta fora das pastas do jogo.");
                if (Directory.Exists(full)) Process.Start("explorer.exe", $"\"{full}\"");
                else if (File.Exists(full)) Process.Start("explorer.exe", $"/select,\"{full}\"");
                return true;
            }

            case "confirm":
                return MessageBox.Show(this, S(a, "text"), "PZ Mod Manager", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes;

            default:
                throw new Exception("Comando desconhecido: " + cmd);
        }
    }

    object BuildState()
    {
        var scan = ModScanner.Scan(_paths);
        var remote = _ws.CachedItems();
        var required = _ws.CachedRequired();
        var items = scan.Items.Select(i =>
        {
            remote.TryGetValue(i.Id, out var r);
            required.TryGetValue(i.Id, out var req);
            bool outdated = r != null && i.LocalTimeUpdated.HasValue && r.TimeUpdated > i.LocalTimeUpdated.Value + 60;
            return new
            {
                id = i.Id,
                title = r?.Title,
                preview = r?.Preview,
                tags = r?.Tags,
                shortDesc = r?.ShortDesc,
                subscriptions = r?.Subscriptions,
                removed = r?.Removed ?? false,
                localTimeUpdated = i.LocalTimeUpdated,
                remoteTimeUpdated = r?.TimeUpdated,
                outdated,
                size = i.SizeOnDisk,
                modKeys = i.ModKeys,
                requiredItems = req,
            };
        }).ToList();
        // títulos de itens obrigatórios que não estão instalados
        var titles = remote.Values.ToDictionary(r => r.Id, r => r.Title);
        return new
        {
            env = new
            {
                steamPath = _paths.SteamPath,
                gamePath = _paths.GamePath,
                gameVersion = _paths.GameVersion,
                buildId = _paths.BuildId,
                workshopPath = _paths.WorkshopContent,
                zomboidPath = _paths.ZomboidPath,
                gameRunning = GamePaths.IsGameRunning(),
                steamRunning = GamePaths.IsSteamRunning(),
            },
            zb = ZombieBuddy.GetStatus(_paths),
            memory = GameMemory.Get(_paths),
            items,
            mods = scan.Mods,
            overlaps = scan.Overlaps,
            profiles = Profiles.List(_paths),
            titles,
        };
    }

    // só os links que a interface usa; qualquer outro steam:// (run, install…) é recusado
    static readonly System.Text.RegularExpressions.Regex AllowedUrl = new(
        @"^(steam://url/CommunityFilePage/\d+|steam://open/downloads|steam://rungameid/108600|https://[^\s""]+)$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    internal static bool IsAllowedUrl(string url) => !string.IsNullOrWhiteSpace(url) && AllowedUrl.IsMatch(url);

    static void OpenExternal(string url)
    {
        if (!IsAllowedUrl(url)) { Log.Write("URL recusada: " + url); return; }
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
