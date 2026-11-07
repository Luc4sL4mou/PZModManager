using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PZModManager.Core;

public sealed class ZbStatus
{
    public bool Subscribed { get; set; }          // item do Workshop baixado
    public bool FilesInGame { get; set; }         // zbNative.dll + ZombieBuddy.jar na pasta do jogo
    public bool JarUpToDate { get; set; }         // jar do jogo == jar do Workshop
    public bool UpdatePending { get; set; }       // ZombieBuddy.jar.new aguardando (zbNative troca sozinho)
    public bool JsonPatched { get; set; }
    public bool BatPatched { get; set; }
    public bool SteamLaunchOption { get; set; }
    public string? WorkshopVersion { get; set; }
    public string State { get; set; } = "";       // ok | not_subscribed | not_installed | needs_repair | needs_update
    public string Message { get; set; } = "";
    public string? ApprovalsFile { get; set; }
}

/// <summary>
/// Faz o mesmo que o ZombieBuddyInstaller oficial (github.com/zed-0xff/ZombieBuddy, installer.go):
/// copia zbNative.dll + ZombieBuddy.jar de mods/ZombieBuddy/libs para a pasta do jogo e adiciona
/// "-agentlib:zbNative" ao ProjectZomboid64.json (inicialização normal) e ao ProjectZomboid64.bat.
/// Mantém a política padrão (perguntar antes de carregar cada mod Java).
/// </summary>
public static class ZombieBuddy
{
    public const string WorkshopId = "3619862853";
    public const string ModId = "ZombieBuddy";
    const string LaunchArg = "-agentlib:zbNative";
    static readonly string[] CoreFiles = { "zbNative.dll", "ZombieBuddy.jar" };
    static readonly object WriteLock = new();

    static string? LibsDir(GamePaths p)
    {
        if (p.WorkshopContent == null) return null;
        var d = Path.Combine(p.WorkshopContent, WorkshopId, "mods", "ZombieBuddy", "libs");
        return Directory.Exists(d) ? d : null;
    }

    static bool IsZbArg(string a) => a == LaunchArg || a.StartsWith(LaunchArg + "=");

    public static ZbStatus GetStatus(GamePaths p)
    {
        var s = new ZbStatus();
        var libs = LibsDir(p);
        s.Subscribed = libs != null && CoreFiles.All(f => File.Exists(Path.Combine(libs, f)));
        if (p.GamePath == null) { s.State = "no_game"; s.Message = "Project Zomboid não encontrado."; return s; }

        s.FilesInGame = CoreFiles.All(f => File.Exists(Path.Combine(p.GamePath, f)));
        s.UpdatePending = File.Exists(Path.Combine(p.GamePath, "ZombieBuddy.jar.new"));
        if (s.Subscribed && s.FilesInGame)
            s.JarUpToDate = SameFile(Path.Combine(libs!, "ZombieBuddy.jar"), Path.Combine(p.GamePath, "ZombieBuddy.jar"))
                            && SameFile(Path.Combine(libs!, "zbNative.dll"), Path.Combine(p.GamePath, "zbNative.dll"));
        s.JsonPatched = JsonHasArg(Path.Combine(p.GamePath, "ProjectZomboid64.json"));
        s.BatPatched = BatHasArg(Path.Combine(p.GamePath, "ProjectZomboid64.bat"));
        s.SteamLaunchOption = SteamLaunchOptionHasArg(p);
        s.WorkshopVersion = ReadWorkshopVersion(p);
        var appr = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zombie_buddy", "mod_approvals.json");
        s.ApprovalsFile = File.Exists(appr) ? appr : null;

        bool launcherOk = s.JsonPatched || s.SteamLaunchOption;
        if (!s.Subscribed && !s.FilesInGame)
        { s.State = "not_subscribed"; s.Message = "Inscreva-se no ZombieBuddy no Workshop para começar."; }
        else if (!s.FilesInGame && !launcherOk)
        { s.State = s.Subscribed ? "not_installed" : "not_subscribed"; s.Message = s.Subscribed ? "Baixado, mas ainda não instalado no jogo." : "Inscreva-se no ZombieBuddy no Workshop."; }
        else if (!s.FilesInGame || !launcherOk)
        { s.State = "needs_repair"; s.Message = !launcherOk
            ? "A configuração do lançador sumiu (geralmente após uma atualização do jogo). Clique em Reparar."
            : "Arquivos do ZombieBuddy faltando na pasta do jogo. Clique em Reparar."; }
        else if (s.Subscribed && !s.JarUpToDate && !s.UpdatePending)
        { s.State = "needs_update"; s.Message = "Há uma versão mais nova do ZombieBuddy baixada pelo Workshop."; }
        else
        { s.State = "ok"; s.Message = "ZombieBuddy instalado e ativo."; }
        return s;
    }

    static string? ReadWorkshopVersion(GamePaths p)
    {
        try
        {
            if (p.WorkshopContent == null) return null;
            var baseDir = Path.Combine(p.WorkshopContent, WorkshopId, "mods", "ZombieBuddy");
            if (!Directory.Exists(baseDir)) return null;
            foreach (var f in Directory.EnumerateFiles(baseDir, "mod.info", SearchOption.AllDirectories))
                foreach (var l in File.ReadLines(f))
                    if (l.StartsWith("modversion=", StringComparison.OrdinalIgnoreCase)) return l[11..].Trim();
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Compara só metadados (tamanho + data de modificação; File.Copy preserva a data).
    /// Não lê o conteúdo de propósito: ler o .jar de 13 MB logo após copiá-lo fazia o antivírus
    /// (Kaspersky) segurar a leitura indefinidamente e o app travava.
    /// </summary>
    static bool SameFile(string a, string b)
    {
        try
        {
            var fa = new FileInfo(a); var fb = new FileInfo(b);
            if (!fa.Exists || !fb.Exists || fa.Length != fb.Length) return false;
            return Math.Abs((fa.LastWriteTimeUtc - fb.LastWriteTimeUtc).TotalSeconds) < 2;
        }
        catch { return false; }
    }

    static bool JsonHasArg(string file)
    {
        try
        {
            if (!File.Exists(file)) return false;
            var root = JsonNode.Parse(File.ReadAllText(file));
            return root?["vmArgs"] is JsonArray arr && arr.Any(x => x is JsonValue v && v.TryGetValue<string>(out var s) && IsZbArg(s));
        }
        catch { return false; }
    }

    static bool BatHasArg(string file)
    {
        try
        {
            if (!File.Exists(file)) return false;
            return File.ReadLines(file).Any(l => l.TrimStart().StartsWith("SET _JAVA_OPTIONS=", StringComparison.OrdinalIgnoreCase)
                                               && l.Contains(LaunchArg));
        }
        catch { return false; }
    }

    static bool SteamLaunchOptionHasArg(GamePaths p)
    {
        try
        {
            if (p.SteamPath == null) return false;
            var ud = Path.Combine(p.SteamPath, "userdata");
            if (!Directory.Exists(ud)) return false;
            foreach (var u in Directory.GetDirectories(ud))
            {
                var lc = Path.Combine(u, "config", "localconfig.vdf");
                if (!File.Exists(lc)) continue;
                var txt = File.ReadAllText(lc);
                // procura rapidamente o bloco do app antes de parsear tudo
                int i = txt.IndexOf("\"" + GamePaths.AppId + "\"", StringComparison.Ordinal);
                if (i < 0) continue;
                var root = VdfNode.Parse(txt);
                var app = root.Path("UserLocalConfigStore", "Software", "Valve", "Steam", "apps", GamePaths.AppId)
                          ?? root.Path("UserLocalConfigStore", "Software", "Valve", "steam", "apps", GamePaths.AppId);
                var lo = app?.Str("LaunchOptions");
                if (lo != null && lo.Contains(LaunchArg)) return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>Instala, atualiza ou repara. Retorna a lista de mudanças feitas.</summary>
    public static List<string> Install(GamePaths p)
    {
        lock (WriteLock) return InstallCore(p);
    }

    static List<string> InstallCore(GamePaths p)
    {
        if (p.GamePath == null) throw new InvalidOperationException("Pasta do jogo não encontrada.");
        if (GamePaths.IsGameRunning()) throw new InvalidOperationException("Feche o Project Zomboid antes de instalar o ZombieBuddy.");
        var libs = LibsDir(p) ?? throw new InvalidOperationException("O ZombieBuddy ainda não foi baixado pelo Workshop. Inscreva-se primeiro.");
        var changes = new List<string>();

        foreach (var f in CoreFiles)
        {
            var src = Path.Combine(libs, f);
            if (!File.Exists(src)) throw new InvalidOperationException($"Arquivo {f} não encontrado no item do Workshop.");
            var dst = Path.Combine(p.GamePath, f);
            if (File.Exists(dst) && SameFile(src, dst)) continue;
            File.Copy(src, dst, true);
            changes.Add($"Copiado {f} para a pasta do jogo");
        }
        var pending = Path.Combine(p.GamePath, "ZombieBuddy.jar.new");
        if (File.Exists(pending)) { File.Delete(pending); changes.Add("Removida atualização pendente antiga (ZombieBuddy.jar.new)"); }

        var json = Path.Combine(p.GamePath, "ProjectZomboid64.json");
        if (File.Exists(json) && !JsonHasArg(json))
        {
            Profiles.Backup(json);
            var root = JsonNode.Parse(File.ReadAllText(json))!.AsObject();
            if (root["vmArgs"] is not JsonArray arr) { arr = new JsonArray(); root["vmArgs"] = arr; }
            arr.Insert(0, LaunchArg);
            Profiles.WriteAtomic(json, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            changes.Add("Adicionado -agentlib:zbNative ao ProjectZomboid64.json");
        }
        var bat = Path.Combine(p.GamePath, "ProjectZomboid64.bat");
        if (File.Exists(bat) && !BatHasArg(bat))
        {
            Profiles.Backup(bat);
            var lines = File.ReadAllLines(bat).ToList();
            int idx = lines.FindIndex(l => l.TrimStart().StartsWith("SET _JAVA_OPTIONS=", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                var cur = lines[idx][(lines[idx].IndexOf('=') + 1)..].Trim();
                lines[idx] = "SET _JAVA_OPTIONS=" + (cur.Length > 0 ? LaunchArg + " " + cur : LaunchArg);
            }
            else
            {
                int at = lines.FindIndex(l => l.TrimStart().StartsWith("@cd", StringComparison.OrdinalIgnoreCase));
                lines.Insert(at >= 0 ? at + 1 : 0, "SET _JAVA_OPTIONS=" + LaunchArg);
            }
            Profiles.WriteAtomic(bat, string.Join("\r\n", lines) + "\r\n");
            changes.Add("Adicionado -agentlib:zbNative ao ProjectZomboid64.bat");
        }
        if (changes.Count == 0) changes.Add("Nada a fazer: já estava tudo certo.");
        return changes;
    }

    public static List<string> Uninstall(GamePaths p)
    {
        lock (WriteLock) return UninstallCore(p);
    }

    static List<string> UninstallCore(GamePaths p)
    {
        if (p.GamePath == null) throw new InvalidOperationException("Pasta do jogo não encontrada.");
        if (GamePaths.IsGameRunning()) throw new InvalidOperationException("Feche o Project Zomboid antes de remover o ZombieBuddy.");
        var changes = new List<string>();
        var json = Path.Combine(p.GamePath, "ProjectZomboid64.json");
        if (JsonHasArg(json))
        {
            Profiles.Backup(json);
            var root = JsonNode.Parse(File.ReadAllText(json))!.AsObject();
            if (root["vmArgs"] is JsonArray arr)
                for (int i = arr.Count - 1; i >= 0; i--)
                    if (arr[i] is JsonValue v && v.TryGetValue<string>(out var s) && IsZbArg(s)) arr.RemoveAt(i);
            Profiles.WriteAtomic(json, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            changes.Add("Removido -agentlib:zbNative do ProjectZomboid64.json");
        }
        var bat = Path.Combine(p.GamePath, "ProjectZomboid64.bat");
        if (BatHasArg(bat))
        {
            var lines = File.ReadAllLines(bat).Select(l =>
                l.TrimStart().StartsWith("SET _JAVA_OPTIONS=", StringComparison.OrdinalIgnoreCase)
                    ? Regex.Replace(l, @"\s*-agentlib:zbNative(=\S*)?", "").Replace("SET _JAVA_OPTIONS= ", "SET _JAVA_OPTIONS=")
                    : l).ToArray();
            Profiles.WriteAtomic(bat, string.Join("\r\n", lines) + "\r\n");
            changes.Add("Removido -agentlib:zbNative do ProjectZomboid64.bat");
        }
        foreach (var f in CoreFiles.Append("ZombieBuddy.jar.new"))
        {
            var d = Path.Combine(p.GamePath, f);
            if (File.Exists(d)) { File.Delete(d); changes.Add($"Apagado {f}"); }
        }
        if (SteamLaunchOptionHasArg(p))
            changes.Add("Atenção: as Opções de inicialização da Steam ainda têm -agentlib:zbNative. Remova manualmente em Propriedades do jogo.");
        if (changes.Count == 0) changes.Add("O ZombieBuddy não estava instalado.");
        return changes;
    }
}
