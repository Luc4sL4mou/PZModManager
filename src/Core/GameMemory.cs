using System.Text;
using System.Text.RegularExpressions;

namespace PZModManager.Core;

/// <summary>Memória máxima do Java do jogo (-Xmx) no ProjectZomboid64.json e .bat.
/// O padrão (3 GB) estoura com mapas grandes e muitos mods Java (OutOfMemoryError ao carregar o mundo).</summary>
public static class GameMemory
{
    static readonly Regex Xmx = new(@"-Xmx(\d+)([mMgG])", RegexOptions.Compiled);

    public static object Get(GamePaths p)
    {
        int? mb = null;
        var json = p.GamePath != null ? Path.Combine(p.GamePath, "ProjectZomboid64.json") : null;
        if (json != null && File.Exists(json))
        {
            var m = Xmx.Match(File.ReadAllText(json));
            if (m.Success) mb = int.Parse(m.Groups[1].Value) * (m.Groups[2].Value.ToLowerInvariant() == "g" ? 1024 : 1);
        }
        var totalMb = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024));
        return new { xmxMb = mb, totalMb };
    }

    public static List<string> Set(GamePaths p, int mb)
    {
        if (p.GamePath == null) throw new Exception("Pasta do jogo não encontrada.");
        if (GamePaths.IsGameRunning()) throw new Exception("Feche o Project Zomboid antes de mudar a memória.");
        var totalMb = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024));
        // deixa pelo menos 4 GB para o Windows, a Steam e a memória fora do heap do jogo
        if (mb < 2048 || mb > totalMb - 4096) throw new Exception($"Valor fora do permitido (2048 a {totalMb - 4096} MB).");
        var changes = new List<string>();
        bool found = false;
        foreach (var name in new[] { "ProjectZomboid64.json", "ProjectZomboid64.bat" })
        {
            var f = Path.Combine(p.GamePath, name);
            if (!File.Exists(f)) continue;
            var txt = File.ReadAllText(f);
            if (!Xmx.IsMatch(txt)) continue;
            found = true;
            var next = Xmx.Replace(txt, $"-Xmx{mb}m");
            if (next == txt) continue;
            Profiles.Backup(f);
            Profiles.WriteAtomic(f, next);
            changes.Add($"{name}: -Xmx{mb}m");
        }
        // sem -Xmx nenhum, nada foi gravado: avisar em vez de mostrar "aplicado"
        if (!found) throw new Exception("Não encontrei a configuração de memória (-Xmx) no lançador do jogo (ProjectZomboid64.json/.bat). " +
                                        "Uma atualização do jogo pode ter mudado o formato; verifique os arquivos do jogo na Steam.");
        return changes;
    }
}
