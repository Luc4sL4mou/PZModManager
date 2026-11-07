namespace PZModManager.Core;

/// <summary>Idioma das caixas de mensagem do Windows (a interface traduz o resto sozinha).
/// Inglês é o padrão; a escolha da interface fica em dados\lang.txt para valer antes de ela carregar.</summary>
public static class Lang
{
    static string FilePath => Path.Combine(GamePaths.DataDir, "lang.txt");

    public static bool En { get; private set; } = Load();

    static bool Load()
    {
        try { return !File.Exists(FilePath) || File.ReadAllText(FilePath).Trim() != "pt"; }
        catch { return true; }
    }

    public static void Set(string lang)
    {
        En = lang != "pt";
        try { File.WriteAllText(FilePath, En ? "en" : "pt"); } catch { }
    }

    public static string T(string pt, string en) => En ? en : pt;
}
