namespace PZModManager.Core;

/// <summary>Volta o lançador do jogo (ProjectZomboid64.json/.bat) para antes da primeira alteração do app.</summary>
public static class Launcher
{
    static readonly string[] Files = { "ProjectZomboid64.json", "ProjectZomboid64.bat" };

    public static List<string> RestoreOriginals(GamePaths p)
    {
        if (p.GamePath == null) throw new InvalidOperationException("Pasta do jogo não encontrada.");
        if (GamePaths.IsGameRunning()) throw new InvalidOperationException("Feche o Project Zomboid antes de restaurar os arquivos originais.");
        var backups = Path.Combine(GamePaths.DataDir, "backups");
        // mesma regra de nome do Profiles.Backup: <pasta do jogo>__<arquivo>
        var prefix = Path.GetFileName(p.GamePath.TrimEnd('\\', '/')) + "__";
        // pastas AAAA-MM-DD: a ordem do nome é a ordem da data, a mais antiga é a de antes da primeira alteração
        var days = Directory.Exists(backups) ? Directory.GetDirectories(backups).OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal).ToList() : new();
        var changes = new List<string>();
        foreach (var name in Files)
        {
            var src = days.Select(d => Path.Combine(d, prefix + name)).FirstOrDefault(File.Exists);
            if (src == null) continue;
            var dst = Path.Combine(p.GamePath, name);
            if (File.Exists(dst) && File.ReadAllBytes(dst).AsSpan().SequenceEqual(File.ReadAllBytes(src))) { changes.Add($"{name}: já era o original"); continue; }
            Profiles.Backup(dst);
            // cópia byte a byte (mesma codificação do original) com a troca atômica do WriteAtomic
            File.Copy(src, dst + ".tmp", true);
            File.Move(dst + ".tmp", dst, true);
            changes.Add($"{name}: restaurado da cópia de {Path.GetFileName(Path.GetDirectoryName(src))}");
        }
        if (changes.Count == 0)
            throw new InvalidOperationException("Não há cópia dos arquivos originais do lançador (o app ainda não os alterou). " +
                                                "Se precisar, use \"Verificar integridade dos arquivos\" do jogo na Steam.");
        return changes;
    }
}
