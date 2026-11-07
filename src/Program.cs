using System.Text.Json;
using PZModManager.Core;

namespace PZModManager;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--selftest")) { SelfTest().GetAwaiter().GetResult(); return; }

        using var mutex = new Mutex(true, "PZModManager_SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("O PZ Mod Manager já está aberto.", "PZ Mod Manager",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Log.Error("UI", e.Exception);
            MessageBox.Show("Ocorreu um erro inesperado:\n\n" + e.Exception.Message + "\n\nDetalhes em dados\\log.txt.",
                "PZ Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("FATAL: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Error("task", e.Exception); e.SetObserved(); };
        Log.Write("=== início v" + Application.ProductVersion);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    /// <summary>Grava em dados\selftest.json o que o app detecta (sem alterar nada no jogo).</summary>
    static async Task SelfTest()
    {
        var p = GamePaths.Detect();
        var ws = new SteamWorkshop();
        var scan = ModScanner.Scan(p);
        var ids = scan.Items.Select(i => i.Id).Append(ZombieBuddy.WorkshopId).Append(Recommendations.ViewpointId).ToList();
        await ws.FetchDetails(ids, false);
        await ws.FetchRequired(new[] { Recommendations.ViewpointId, "3811256204" }, false);
        var addons = await ws.FetchViewpointAddons(false);
        var profiles = Profiles.List(p);
        var result = new
        {
            paths = p,
            zb = ZombieBuddy.GetStatus(p),
            scan,
            profiles,
            profileData = profiles.Select(Profiles.Read).ToList(),
            required = ws.CachedRequired(),
            addonsCount = addons.Count,
            details = ws.CachedItems().Where(kv => ids.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value.Title),
        };
        File.WriteAllText(Path.Combine(GamePaths.DataDir, "selftest.json"),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
}
