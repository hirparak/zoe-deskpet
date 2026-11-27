namespace Zoe.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Test hooks: --diag logs Zoe's state every second; --exit-after N quits after N seconds.
        PetForm.Diag = args.Contains("--diag");
        int i = Array.IndexOf(args, "--exit-after");
        if (i >= 0 && i + 1 < args.Length && double.TryParse(args[i + 1], out double secs)) PetForm.ExitAfter = secs;

        using var single = new Mutex(true, @"Local\ZoeDeskPet", out bool first);
        if (!first) return;

        Application.ThreadException += (_, e) => Log.Write("ui error: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("fatal: " + e.ExceptionObject);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new PetForm(AppContext.BaseDirectory));
    }
}
