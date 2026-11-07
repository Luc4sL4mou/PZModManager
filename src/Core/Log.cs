namespace PZModManager.Core;

/// <summary>Log simples em dados\log.txt (rotaciona em 1 MB).</summary>
public static class Log
{
    static readonly object Lock = new();
    static string FilePath => Path.Combine(GamePaths.DataDir, "log.txt");

    public static void Write(string msg)
    {
        try
        {
            lock (Lock)
            {
                var f = FilePath;
                if (File.Exists(f) && new FileInfo(f).Length > 1_000_000)
                    File.Move(f, f + ".old", true);
                File.AppendAllText(f, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [t{Environment.CurrentManagedThreadId}] {msg}{Environment.NewLine}");
            }
        }
        catch { }
    }

    public static void Error(string where, Exception ex) => Write($"ERRO em {where}: {ex}");
}
