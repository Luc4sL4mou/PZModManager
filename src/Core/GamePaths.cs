using System.Diagnostics;
using Microsoft.Win32;

namespace PZModManager.Core;

public sealed class GamePaths
{
    public const string AppId = "108600";

    public string? SteamPath { get; private set; }
    public string? GameLibrary { get; private set; }
    public string? GamePath { get; private set; }
    public string? WorkshopContent { get; private set; }
    public string? WorkshopAcf { get; private set; }
    public string ZomboidPath { get; private set; } = "";
    public string? GameVersion { get; private set; }
    public string? BuildId { get; private set; }
    public List<string> Libraries { get; private set; } = new();

    public static GamePaths Detect()
    {
        var p = new GamePaths();
        p.SteamPath = FindSteam();
        var libs = new List<string>();
        if (p.SteamPath != null)
        {
            libs.Add(p.SteamPath);
            var lf = VdfNode.TryLoad(System.IO.Path.Combine(p.SteamPath, "steamapps", "libraryfolders.vdf"));
            var folders = lf?["libraryfolders"];
            if (folders != null)
                foreach (var f in folders.Children.Values)
                    if (f.Str("path") is string lp && !libs.Contains(lp, StringComparer.OrdinalIgnoreCase))
                        libs.Add(lp);
        }
        p.Libraries = libs;
        foreach (var lib in libs)
        {
            var acfPath = System.IO.Path.Combine(lib, "steamapps", $"appmanifest_{AppId}.acf");
            if (!File.Exists(acfPath)) continue;
            var acf = VdfNode.TryLoad(acfPath)?["AppState"];
            var installDir = acf?.Str("installdir") ?? "ProjectZomboid";
            p.GameLibrary = lib;
            p.GamePath = System.IO.Path.Combine(lib, "steamapps", "common", installDir);
            p.BuildId = acf?.Str("buildid");
            p.WorkshopContent = System.IO.Path.Combine(lib, "steamapps", "workshop", "content", AppId);
            p.WorkshopAcf = System.IO.Path.Combine(lib, "steamapps", "workshop", $"appworkshop_{AppId}.acf");
            break;
        }
        p.ZomboidPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid");
        p.GameVersion = ReadGameVersion(p.ZomboidPath);
        return p;
    }

    static string? FindSteam()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (k?.GetValue("SteamPath") is string sp && Directory.Exists(sp))
                return System.IO.Path.GetFullPath(sp);
        }
        catch { }
        foreach (var c in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" })
            if (Directory.Exists(c)) return c;
        return null;
    }

    static string? ReadGameVersion(string zomboid)
    {
        try
        {
            var f = System.IO.Path.Combine(zomboid, "version.txt");
            if (!File.Exists(f)) return null;
            var first = File.ReadLines(f).FirstOrDefault()?.Trim();
            return first?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        catch { return null; }
    }

    /// <summary>Versão do jogo como (major, minor). Padrão 42.0 se desconhecida.</summary>
    public (int major, int minor) VersionTuple()
    {
        var v = ParseVersion(GameVersion);
        return v ?? (42, 0);
    }

    public static (int major, int minor)? ParseVersion(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Trim().Split('.');
        if (!int.TryParse(parts[0], out int major)) return null;
        int minor = 0;
        if (parts.Length > 1 && !int.TryParse(parts[1], out minor)) return null;
        return (major, minor);
    }

    // o lançador pode virar o java.exe do próprio jogo (jre64), sem processo ProjectZomboid64
    public static bool IsGameRunning() =>
        new[] { "ProjectZomboid64", "ProjectZomboid32", "java", "javaw" }
            .SelectMany(Process.GetProcessesByName).Any(IsZomboidProcess);

    static bool IsZomboidProcess(Process p)
    {
        using (p)
        {
            if (p.ProcessName.StartsWith("ProjectZomboid", StringComparison.OrdinalIgnoreCase)) return true;
            try { return p.MainModule?.FileName.Contains(@"\ProjectZomboid\", StringComparison.OrdinalIgnoreCase) == true; }
            catch { return false; } // processo de outro usuário/elevado: não é o nosso jogo
        }
    }

    public static bool IsSteamRunning() => Process.GetProcessesByName("steam").Length > 0;

    /// <summary>Dados locais do app (cache, backups, WebView2) ficam ao lado do exe.</summary>
    public static string DataDir
    {
        get
        {
            var d = System.IO.Path.Combine(AppContext.BaseDirectory, "dados");
            Directory.CreateDirectory(d);
            return d;
        }
    }
}
