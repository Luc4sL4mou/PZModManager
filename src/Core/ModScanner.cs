namespace PZModManager.Core;

public sealed class ModInfo
{
    public string Key { get; set; } = "";          // workshopId\modId ou local\modId
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Folder { get; set; } = "";
    public string Path { get; set; } = "";
    public string? WorkshopId { get; set; }
    public string Source { get; set; } = "workshop"; // workshop | local
    public string Description { get; set; } = "";
    public string? Author { get; set; }
    public string? ModVersion { get; set; }
    public string? Category { get; set; }
    public string? Url { get; set; }
    public string? VersionDir { get; set; }
    public bool B42Ok { get; set; }
    public string? VersionMin { get; set; }
    public string? VersionMax { get; set; }
    public bool HasMaps { get; set; }
    public List<string> MapNames { get; set; } = new();
    public bool HasJava { get; set; }
    public List<string> Requires { get; set; } = new();
    public List<string> Incompatible { get; set; } = new();
    public List<string> LoadAfter { get; set; } = new();
    public List<string> LoadBefore { get; set; } = new();
    public string? PosterPath { get; set; }
}

public sealed class WorkshopItemLocal
{
    public string Id { get; set; } = "";
    public long? LocalTimeUpdated { get; set; }
    public long? SizeOnDisk { get; set; }
    public List<string> ModKeys { get; set; } = new();
}

public sealed class FileOverlap
{
    public string Kind { get; set; } = "file";     // file | script | map
    public string File { get; set; } = "";
    public List<string> Mods { get; set; } = new();
}

public sealed class ScanResult
{
    public List<ModInfo> Mods { get; set; } = new();
    public List<WorkshopItemLocal> Items { get; set; } = new();
    public List<FileOverlap> Overlaps { get; set; } = new();
}

public static class ModScanner
{
    public static ScanResult Scan(GamePaths paths)
    {
        var res = new ScanResult();
        var (gMaj, gMin) = paths.VersionTuple();
        var filesByMod = new Dictionary<string, HashSet<string>>();

        // Dados do appworkshop (datas locais)
        var acf = paths.WorkshopAcf != null ? VdfNode.TryLoad(paths.WorkshopAcf)?["AppWorkshop"] : null;
        var installed = acf?["WorkshopItemsInstalled"];

        if (paths.WorkshopContent != null && Directory.Exists(paths.WorkshopContent))
        {
            foreach (var dir in Directory.GetDirectories(paths.WorkshopContent))
            {
                var wid = System.IO.Path.GetFileName(dir);
                if (!wid.All(char.IsDigit)) continue;
                var item = new WorkshopItemLocal { Id = wid };
                var node = installed?[wid];
                if (long.TryParse(node?.Str("timeupdated"), out var tu)) item.LocalTimeUpdated = tu;
                if (long.TryParse(node?.Str("size"), out var sz)) item.SizeOnDisk = sz;
                var modsDir = System.IO.Path.Combine(dir, "mods");
                if (Directory.Exists(modsDir))
                {
                    foreach (var md in Directory.GetDirectories(modsDir))
                    {
                        var mi = TryReadModFolder(md, wid, "workshop", gMaj, gMin, filesByMod);
                        if (mi == null) continue;
                        res.Mods.Add(mi);
                        item.ModKeys.Add(mi.Key);
                    }
                }
                res.Items.Add(item);
            }
        }

        // Mods locais (Zomboid\mods) e projetos de upload (Zomboid\Workshop\*\Contents\mods)
        var localRoots = new List<string> { System.IO.Path.Combine(paths.ZomboidPath, "mods") };
        var userWs = System.IO.Path.Combine(paths.ZomboidPath, "Workshop");
        if (Directory.Exists(userWs))
            foreach (var w in Directory.GetDirectories(userWs))
                localRoots.Add(System.IO.Path.Combine(w, "Contents", "mods"));
        foreach (var root in localRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var md in Directory.GetDirectories(root))
            {
                if (System.IO.Path.GetFileName(md).Equals("examplemod", StringComparison.OrdinalIgnoreCase)) continue;
                var mi = TryReadModFolder(md, null, "local", gMaj, gMin, filesByMod);
                if (mi != null) res.Mods.Add(mi);
            }
        }

        // Arquivos sobrescritos por mais de um mod (possíveis conflitos)
        var owners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, files) in filesByMod)
            foreach (var f in files)
            {
                if (!owners.TryGetValue(f, out var l)) owners[f] = l = new List<string>();
                l.Add(key);
            }
        foreach (var (f, l) in owners)
            if (l.Count > 1) res.Overlaps.Add(new FileOverlap { File = f, Mods = l });

        return res;
    }

    static int VerInt(int maj, int min) => maj * 1000 + Math.Min(min, 999);

    static (int, int)? ParseFolderVersion(string name)
    {
        var parts = name.Split('.');
        if (parts.Length is < 1 or > 3) return null;
        if (!int.TryParse(parts[0], out int maj)) return null;
        int min = 0;
        if (parts.Length > 1 && !int.TryParse(parts[1], out min)) return null;
        if (parts.Length > 2 && !int.TryParse(parts[2], out _)) return null;
        return (maj, min);
    }

    /// <summary>Um mod ilegível (permissão, pasta sendo apagada pela Steam) é pulado em vez de derrubar a lista toda.</summary>
    static ModInfo? TryReadModFolder(string modDir, string? wid, string source, int gMaj, int gMin,
        Dictionary<string, HashSet<string>> filesByMod)
    {
        try { return ReadModFolder(modDir, wid, source, gMaj, gMin, filesByMod); }
        catch (Exception ex) { Log.Error("ler mod " + modDir, ex); return null; }
    }

    static ModInfo? ReadModFolder(string modDir, string? wid, string source, int gMaj, int gMin,
        Dictionary<string, HashSet<string>> filesByMod)
    {
        // Igual ao jogo: maior pasta de versão <= versão do jogo (e >= 42)
        string? versionDir = null;
        int best = -1;
        int gameInt = VerInt(gMaj, gMin), minInt = VerInt(42, 0);
        foreach (var sub in Directory.GetDirectories(modDir))
        {
            var v = ParseFolderVersion(System.IO.Path.GetFileName(sub));
            if (v == null) continue;
            int vi = VerInt(v.Value.Item1, v.Value.Item2);
            if (vi > gameInt || vi < minInt) continue;
            if (vi > best) { best = vi; versionDir = sub; }
        }
        var commonDir = System.IO.Path.Combine(modDir, "common");

        // Igual ao jogo (ChooseGameInfo.readModInfoAux): <versão>/mod.info, senão common/mod.info.
        // mod.info na raiz é do formato B41 e o B42 ignora.
        string? infoPath = null;
        bool b42 = false;
        if (versionDir != null && File.Exists(System.IO.Path.Combine(versionDir, "mod.info")))
        {
            infoPath = System.IO.Path.Combine(versionDir, "mod.info");
            b42 = true;
        }
        else if (File.Exists(System.IO.Path.Combine(commonDir, "mod.info")))
        {
            infoPath = System.IO.Path.Combine(commonDir, "mod.info");
            b42 = true;
        }
        else if (File.Exists(System.IO.Path.Combine(modDir, "mod.info")))
            infoPath = System.IO.Path.Combine(modDir, "mod.info");
        if (infoPath == null) return null;

        var mi = new ModInfo
        {
            Folder = System.IO.Path.GetFileName(modDir),
            Path = modDir,
            WorkshopId = wid,
            Source = source,
            VersionDir = versionDir != null ? System.IO.Path.GetFileName(versionDir) : null,
            B42Ok = b42,
        };
        ParseModInfo(infoPath, mi);
        if (string.IsNullOrWhiteSpace(mi.Id)) mi.Id = mi.Folder;
        if (string.IsNullOrWhiteSpace(mi.Name)) mi.Name = mi.Id;
        mi.Key = (wid ?? "local") + "\\" + mi.Id;

        var infoDir = System.IO.Path.GetDirectoryName(infoPath)!;
        if (mi.PosterPath != null)
        {
            var pp = System.IO.Path.Combine(infoDir, mi.PosterPath);
            mi.PosterPath = File.Exists(pp) ? pp : null;
        }

        // Varre media/ (common + versão) para mapas, java e arquivos que sobrescrevem
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>();
        if (b42)
        {
            if (Directory.Exists(commonDir)) roots.Add(commonDir);
            if (versionDir != null) roots.Add(versionDir);
        }
        else roots.Add(modDir);
        foreach (var r in roots)
        {
            var media = System.IO.Path.Combine(r, "media");
            if (!Directory.Exists(media)) continue;
            var maps = System.IO.Path.Combine(media, "maps");
            if (Directory.Exists(maps))
                foreach (var m in Directory.GetDirectories(maps))
                    if (File.Exists(System.IO.Path.Combine(m, "map.info")))
                    {
                        mi.HasMaps = true;
                        var mn = System.IO.Path.GetFileName(m);
                        if (!mi.MapNames.Contains(mn)) mi.MapNames.Add(mn);
                    }
            if (Directory.Exists(System.IO.Path.Combine(media, "java"))) mi.HasJava = true;
            try
            {
                foreach (var sub in new[] { "lua", "scripts" })
                {
                    var d = System.IO.Path.Combine(media, sub);
                    if (!Directory.Exists(d)) continue;
                    foreach (var f in Directory.EnumerateFiles(d, "*.*", SearchOption.AllDirectories))
                    {
                        var ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                        if (ext != ".lua" && ext != ".txt") continue;
                        var rel = System.IO.Path.GetRelativePath(r, f).Replace('\\', '/');
                        // traduções sempre coincidem; não são conflito
                        if (rel.Contains("/Translate/", StringComparison.OrdinalIgnoreCase)) continue;
                        files.Add(rel);
                    }
                }
            }
            catch { }
        }
        if (Directory.EnumerateFiles(roots.Last(), "*.jar", SearchOption.TopDirectoryOnly).Any() ||
            Directory.Exists(System.IO.Path.Combine(roots.Last(), "java"))) mi.HasJava = true;
        filesByMod[mi.Key] = files;
        return mi;
    }

    static List<string> SplitIds(string v) =>
        v.Replace("\\", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Where(x => x.Length > 0).ToList();

    static void ParseModInfo(string file, ModInfo mi)
    {
        var desc = new List<string>();
        foreach (var raw in File.ReadLines(file))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var k = line[..eq].Trim().ToLowerInvariant();
            var v = line[(eq + 1)..].Trim();
            switch (k)
            {
                case "id": mi.Id = v.Replace("\\", "").Trim(); break;
                case "name": mi.Name = v; break;
                case "description": desc.Add(v); break;
                case "author": mi.Author = v; break;
                case "modversion": mi.ModVersion = v; break;
                case "category": mi.Category = v; break;
                case "url": mi.Url = v; break;
                case "poster": mi.PosterPath ??= v; break;
                case "require": mi.Requires.AddRange(SplitIds(v)); break;
                case "incompatible": mi.Incompatible.AddRange(SplitIds(v)); break;
                case "loadmodafter": mi.LoadAfter.AddRange(SplitIds(v)); break;
                case "loadmodbefore": mi.LoadBefore.AddRange(SplitIds(v)); break;
                case "versionmin": mi.VersionMin = v; break;
                case "versionmax": mi.VersionMax = v; break;
                case "javajarfile": mi.HasJava = true; break;
            }
        }
        mi.Description = string.Join("\n", desc).Replace("<br>", "\n").Replace("<BR>", "\n");
        if (mi.Description.Length > 1500) mi.Description = mi.Description[..1500] + "…";
        mi.Requires = mi.Requires.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
