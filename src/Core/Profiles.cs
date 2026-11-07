using System.Text;

namespace PZModManager.Core;

public sealed class ProfileRef
{
    public string Key { get; set; } = "";      // default | save:<mode>/<name> | server:<name>
    public string Type { get; set; } = "";     // default | save | server
    public string Label { get; set; } = "";
    public string Group { get; set; } = "";
    public string File { get; set; } = "";
    public long Modified { get; set; }
}

public sealed class ProfileData
{
    public string Key { get; set; } = "";
    public string Type { get; set; } = "";
    public List<string> Active { get; set; } = new();
    public List<string> Maps { get; set; } = new();
    public List<string> WorkshopItems { get; set; } = new();
    public bool Exists { get; set; }
}

/// <summary>
/// Formatos verificados no código do jogo (B42.21):
///  - default.txt / mods.txt: bloco "mods { mod = ID, }"; o jogo remove "\" ao ler.
///  - servidor .ini: "Mods=ID;ID" (a tela do jogo grava IDs puros) e "WorkshopItems=id;id".
/// </summary>
public static class Profiles
{
    public static List<ProfileRef> List(GamePaths p)
    {
        var list = new List<ProfileRef>
        {
            new()
            {
                Key = "default", Type = "default", Group = "Single player",
                Label = "Padrão (novos jogos)",
                File = System.IO.Path.Combine(p.ZomboidPath, "mods", "default.txt"),
            }
        };
        var saves = System.IO.Path.Combine(p.ZomboidPath, "Saves");
        if (Directory.Exists(saves))
        {
            foreach (var modeDir in Directory.GetDirectories(saves))
            {
                var mode = System.IO.Path.GetFileName(modeDir);
                if (mode.Equals("Multiplayer", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var save in Directory.GetDirectories(modeDir))
                {
                    var name = System.IO.Path.GetFileName(save);
                    var f = System.IO.Path.Combine(save, "mods.txt");
                    if (!System.IO.File.Exists(f) && !System.IO.File.Exists(System.IO.Path.Combine(save, "map_t.bin")))
                        continue;
                    list.Add(new ProfileRef
                    {
                        Key = $"save:{mode}/{name}", Type = "save", Group = "Saves",
                        Label = $"{name} ({mode})", File = f,
                        Modified = Directory.GetLastWriteTimeUtc(save).Ticks,
                    });
                }
            }
        }
        var server = System.IO.Path.Combine(p.ZomboidPath, "Server");
        if (Directory.Exists(server))
            foreach (var ini in Directory.GetFiles(server, "*.ini"))
                list.Add(new ProfileRef
                {
                    Key = "server:" + System.IO.Path.GetFileNameWithoutExtension(ini), Type = "server",
                    Group = "Servidores (hospedar)", Label = System.IO.Path.GetFileNameWithoutExtension(ini), File = ini,
                    Modified = System.IO.File.GetLastWriteTimeUtc(ini).Ticks,
                });
        // saves mais recentes primeiro
        return list.OrderBy(x => x.Type == "default" ? 0 : x.Type == "save" ? 1 : 2)
                   .ThenByDescending(x => x.Modified).ToList();
    }

    public static ProfileRef? Find(GamePaths p, string key) => List(p).FirstOrDefault(x => x.Key == key);

    public static ProfileData Read(ProfileRef r)
    {
        var d = new ProfileData { Key = r.Key, Type = r.Type, Exists = System.IO.File.Exists(r.File) };
        if (!d.Exists) return d;
        var text = System.IO.File.ReadAllText(r.File);
        if (r.Type == "server")
        {
            foreach (var line in text.Split('\n'))
            {
                var l = line.TrimEnd('\r');
                if (l.StartsWith("Mods=")) d.Active = SplitSemi(l[5..]);
                else if (l.StartsWith("WorkshopItems=")) d.WorkshopItems = SplitSemi(l[14..]);
                else if (l.StartsWith("Map=")) d.Maps = SplitSemi(l[4..]);
            }
        }
        else
        {
            d.Active = ReadBlock(text, "mods", "mod");
            d.Maps = ReadBlock(text, "maps", "map");
        }
        return d;
    }

    static List<string> SplitSemi(string v) =>
        v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Select(x => x.Replace("\\", "")).Where(x => x.Length > 0).ToList();

    static List<string> ReadBlock(string text, string block, string key)
    {
        var res = new List<string>();
        var lines = text.Replace("\r", "").Split('\n');
        bool inBlock = false, seenHeader = false;
        foreach (var raw in lines)
        {
            var l = raw.Trim();
            if (!inBlock)
            {
                if (l.Equals(block, StringComparison.OrdinalIgnoreCase)) { seenHeader = true; continue; }
                if (seenHeader && l == "{") { inBlock = true; continue; }
                if (l.Length > 0) seenHeader = false;
                continue;
            }
            if (l == "}") { inBlock = false; seenHeader = false; continue; }
            int eq = l.IndexOf('=');
            if (eq <= 0) continue;
            if (!l[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            var v = l[(eq + 1)..].Trim().TrimEnd(',').Trim().Replace("\\", "");
            if (v.Length > 0) res.Add(v);
        }
        return res;
    }

    static readonly object WriteLock = new();

    public static void Write(GamePaths p, ProfileRef r, List<string> active, List<string>? workshopItems, List<string>? maps)
    {
        // os valores vão crus para mods.txt / .ini: quebra de linha ou separador criaria linhas/entradas novas
        foreach (var v in active.Concat(maps ?? new()))
            if (!IsSafeEntry(v)) throw new ArgumentException($"Nome inválido: \"{v}\".");
        foreach (var w in workshopItems ?? new())
            if (w.Length == 0 || !w.All(char.IsAsciiDigit)) throw new ArgumentException($"ID do Workshop inválido: \"{w}\".");
        lock (WriteLock) WriteCore(r, active, workshopItems, maps);
    }

    internal static bool IsSafeEntry(string v) =>
        v.Trim().Length > 0 && v.IndexOfAny(new[] { '\r', '\n', ';', '=', '{', '}', '\\' }) < 0 && !v.TrimEnd().EndsWith(','); // vírgula no meio é válida ("Muldraugh, KY")

    /// <summary>Grava num .tmp e troca no fim: se cair no meio, o arquivo original fica inteiro.</summary>
    public static void WriteAtomic(string file, string text, Encoding? enc = null)
    {
        var tmp = file + ".tmp";
        System.IO.File.WriteAllText(tmp, text, enc ?? new UTF8Encoding(false));
        System.IO.File.Move(tmp, file, true);
    }

    static void WriteCore(ProfileRef r, List<string> active, List<string>? workshopItems, List<string>? maps)
    {
        Backup(r.File);
        if (r.Type == "server")
        {
            var lines = System.IO.File.Exists(r.File)
                ? System.IO.File.ReadAllText(r.File).Replace("\r\n", "\n").Split('\n').ToList()
                : new List<string>();
            SetIni(lines, "Mods", string.Join(";", active));
            if (workshopItems != null) SetIni(lines, "WorkshopItems", string.Join(";", workshopItems));
            if (maps != null && maps.Count > 0) SetIni(lines, "Map", string.Join(";", maps));
            WriteAtomic(r.File, string.Join("\r\n", lines));
        }
        else
        {
            var existingMaps = maps ?? (System.IO.File.Exists(r.File) ? Read(r).Maps : new List<string>());
            var sb = new StringBuilder();
            sb.Append("VERSION = 1,\r\n\r\nmods\r\n{\r\n");
            foreach (var id in active) sb.Append("    mod = ").Append(id).Append(",\r\n");
            sb.Append("}\r\n\r\nmaps\r\n{\r\n");
            foreach (var m in existingMaps) sb.Append("    map = ").Append(m).Append(",\r\n");
            sb.Append("}\r\n");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(r.File)!);
            WriteAtomic(r.File, sb.ToString());
        }
    }

    static void SetIni(List<string> lines, string key, string value)
    {
        for (int i = 0; i < lines.Count; i++)
            if (lines[i].StartsWith(key + "=")) { lines[i] = key + "=" + value; return; }
        lines.Add(key + "=" + value);
    }

    /// <summary>Copia o arquivo para dados\backups\AAAA-MM-DD\ uma vez por dia antes de alterar.</summary>
    public static void Backup(string file)
    {
        try
        {
            if (!System.IO.File.Exists(file)) return;
            var dir = System.IO.Path.Combine(GamePaths.DataDir, "backups", DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(dir);
            var parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(file)) ?? "";
            var dest = System.IO.Path.Combine(dir, parent + "__" + System.IO.Path.GetFileName(file));
            if (!System.IO.File.Exists(dest)) System.IO.File.Copy(file, dest);
        }
        catch { }
    }
}
