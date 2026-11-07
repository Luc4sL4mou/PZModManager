using System.Text;
using System.Text.RegularExpressions;

namespace PZModManager.Core;

public sealed class SteamQueueStatus
{
    /// <summary>Itens do PZ em que o usuário se inscreveu (segundo o log da Steam) e não cancelou depois.</summary>
    public List<string> Subscribed { get; set; } = new();
    /// <summary>Estado do Project Zomboid na fila da Steam: idle | queued | downloading | suspended</summary>
    public string PzState { get; set; } = "idle";
    public string? PzStateRaw { get; set; }
    /// <summary>Outro jogo que está ocupando a fila de downloads (a Steam baixa um por vez).</summary>
    public string? BusyAppId { get; set; }
    public string? BusyAppName { get; set; }
}

/// <summary>
/// Lê o fim dos logs da Steam (logs\workshop_log.txt e logs\content_log.txt).
/// A Steam não tem API local para isso; os logs são a forma confiável de saber
/// se a inscrição foi registrada e se o download está na fila atrás de outro jogo.
/// </summary>
public static class SteamStatus
{
    static readonly Regex SubRx = new(@"\[AppID 108600\] (Subscribed to|Unsubscribed from) item (\d+)", RegexOptions.Compiled);
    static readonly Regex StateRx = new(@"AppID (\d+) state changed : (.*)$", RegexOptions.Compiled | RegexOptions.Multiline);

    public static SteamQueueStatus Get(GamePaths p, DateTime since)
    {
        var st = new SteamQueueStatus();
        if (p.SteamPath == null) return st;
        var logs = Path.Combine(p.SteamPath, "logs");

        var ws = Tail(Path.Combine(logs, "workshop_log.txt"), 256 * 1024);
        var subs = new List<string>();
        foreach (var line in ws.Split('\n'))
        {
            if (!TryTime(line, out var t) || t < since) continue;
            var m = SubRx.Match(line);
            if (!m.Success) continue;
            var id = m.Groups[2].Value;
            if (m.Groups[1].Value.StartsWith("Sub")) { if (!subs.Contains(id)) subs.Add(id); }
            else subs.Remove(id);
        }
        st.Subscribed = subs;

        var content = Tail(Path.Combine(logs, "content_log.txt"), 512 * 1024);
        var last = new Dictionary<string, string>();
        foreach (Match m in StateRx.Matches(content)) last[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        if (last.TryGetValue(GamePaths.AppId, out var pz))
        {
            st.PzStateRaw = pz;
            st.PzState = pz.Contains("Suspended") ? "suspended"
                : pz.Contains("Update Running") ? "downloading"
                : pz.Contains("Update Queued") ? "queued" : "idle";
        }
        foreach (var (app, state) in last)
        {
            if (app == GamePaths.AppId) continue;
            if (state.Contains("Update Running") && !state.Contains("Suspended"))
            {
                st.BusyAppId = app;
                st.BusyAppName = AppName(p, app) ?? ("AppID " + app);
                break;
            }
        }
        return st;
    }

    static bool TryTime(string line, out DateTime t)
    {
        t = default;
        return line.Length > 21 && line[0] == '[' &&
               DateTime.TryParseExact(line.Substring(1, 19), "yyyy-MM-dd HH:mm:ss", null,
                   System.Globalization.DateTimeStyles.AssumeLocal, out t);
    }

    static string? AppName(GamePaths p, string appId)
    {
        foreach (var lib in p.Libraries)
        {
            var acf = VdfNode.TryLoad(Path.Combine(lib, "steamapps", $"appmanifest_{appId}.acf"));
            var name = acf?["AppState"]?.Str("name");
            if (name != null) return name;
        }
        return null;
    }

    /// <summary>Lê só o final do arquivo, sem bloquear a Steam que está escrevendo nele.</summary>
    static string Tail(string file, int bytes)
    {
        try
        {
            if (!File.Exists(file)) return "";
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var len = fs.Length;
            var start = Math.Max(0, len - bytes);
            fs.Seek(start, SeekOrigin.Begin);
            var buf = new byte[len - start];
            int read = 0;
            while (read < buf.Length)
            {
                int n = fs.Read(buf, read, buf.Length - read);
                if (n <= 0) break;
                read += n;
            }
            var s = Encoding.UTF8.GetString(buf, 0, read);
            if (start > 0) { int nl = s.IndexOf('\n'); if (nl >= 0) s = s[(nl + 1)..]; }
            return s.Replace("\r", "");
        }
        catch { return ""; }
    }
}
