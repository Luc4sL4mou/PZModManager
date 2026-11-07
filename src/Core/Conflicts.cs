using System.Text.RegularExpressions;

namespace PZModManager.Core;

/// <summary>Pontos que dois ou mais mods definem ao mesmo tempo (o último a carregar vence):
/// objetos de script (item, veículo, receita…) e células de mapa (.lotheader).</summary>
public static class Conflicts
{
    static readonly Regex Module = new(@"^\s*module\s+([A-Za-z0-9_]+)", RegexOptions.Compiled);
    static readonly Regex Obj = new(@"^\s*((?:template\s+)?(?:item|vehicle|recipe|craftRecipe|fixing|evolvedrecipe|model|sound|entity))\s+([^{\s][^{]*)",  // sem lazy/âncora: tempo linear (o nome é aparado no código)
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<FileOverlap> Scan(IEnumerable<ModInfo> mods)
    {
        var owners = new Dictionary<string, (string Kind, string Name, List<string> Mods)>(StringComparer.OrdinalIgnoreCase);
        void Add(string kind, string name, string modKey)
        {
            var k = kind + "|" + name;
            if (!owners.TryGetValue(k, out var o)) owners[k] = o = (kind, name, new List<string>());
            if (!o.Mods.Contains(modKey)) o.Mods.Add(modKey);
        }
        foreach (var m in mods)
        {
            foreach (var root in Roots(m))
            {
                var media = Path.Combine(root, "media");
                try
                {
                    var scripts = Path.Combine(media, "scripts");
                    if (Directory.Exists(scripts))
                        foreach (var f in Directory.EnumerateFiles(scripts, "*.txt", SearchOption.AllDirectories))
                            foreach (var (kind, name) in ScriptObjects(f)) Add("script", kind + " " + name, m.Key);
                    var maps = Path.Combine(media, "maps");
                    if (Directory.Exists(maps))
                        foreach (var md in Directory.EnumerateDirectories(maps))
                            foreach (var f in Directory.EnumerateFiles(md, "*.lotheader"))
                                Add("map", Path.GetFileNameWithoutExtension(f), m.Key);
                }
                catch (Exception ex) { Log.Error("conflicts " + m.Key, ex); }
            }
        }
        return owners.Values.Where(o => o.Mods.Count > 1)
            .Select(o => new FileOverlap { Kind = o.Kind, File = o.Name, Mods = o.Mods }).ToList();
    }

    static IEnumerable<string> Roots(ModInfo m)
    {
        if (!m.B42Ok) { yield return m.Path; yield break; }
        var common = Path.Combine(m.Path, "common");
        if (Directory.Exists(common)) yield return common;
        if (m.VersionDir != null) yield return Path.Combine(m.Path, m.VersionDir);
    }

    /// <summary>Objetos no primeiro nível de cada módulo ("Base.Axe"); blocos internos (ex.: model dentro de vehicle) são ignorados.</summary>
    internal static IEnumerable<(string Kind, string Name)> ScriptObjects(string file)
    {
        var res = new List<(string, string)>();
        string module = "";
        int depth = 0;
        bool comment = false;
        foreach (var raw in File.ReadLines(file))
        {
            var line = raw;
            // shortcut: comentários /* */ e // tratados por linha; suficiente para scripts do PZ
            if (comment) { var e = line.IndexOf("*/"); if (e < 0) continue; line = line[(e + 2)..]; comment = false; }
            var s = line.IndexOf("/*");
            if (s >= 0) { var e = line.IndexOf("*/", s + 2); if (e < 0) { comment = true; line = line[..s]; } else line = line[..s] + line[(e + 2)..]; }
            var c = line.IndexOf("//"); if (c >= 0) line = line[..c];
            if (depth == 0)
            {
                var mm = Module.Match(line);
                if (mm.Success)
                {
                    module = mm.Groups[1].Value;
                    var b = line.IndexOf('{');
                    if (b >= 0) { depth = 1; line = line[(b + 1)..]; } // "module X { item Y {} }" na mesma linha
                }
            }
            if (depth == 1)
            {
                var om = Obj.Match(line);
                if (om.Success)
                {
                    var name = om.Groups[2].Value.TrimEnd();
                    res.Add((Regex.Replace(om.Groups[1].Value.ToLowerInvariant(), @"\s+", " "), name.Contains('.') ? name : module + "." + name));
                }
            }
            foreach (var ch in line) { if (ch == '{') depth++; else if (ch == '}' && depth > 0) depth--; }
        }
        return res;
    }
}
