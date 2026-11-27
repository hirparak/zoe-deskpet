using System.Text.Json;

namespace Zoe.App;

/// <summary>The choices her owner makes from the tray menu, kept per user at %LOCALAPPDATA%\Zoe\prefs.json
/// (zoe.json lives in Program Files, which she can't write to).</summary>
internal sealed class Prefs
{
    public bool Autostart { get; set; } = true;
    /// <summary>Liveliness picked from the menu; null = whatever zoe.json says.</summary>
    public double? Energy { get; set; }

    private static readonly string PathName =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zoe", "prefs.json");

    public static Prefs Load()
    {
        try { if (File.Exists(PathName)) return JsonSerializer.Deserialize<Prefs>(File.ReadAllText(PathName)) ?? new Prefs(); }
        catch (Exception ex) { Log.Write("bad prefs.json, using defaults: " + ex.Message); }
        return new Prefs();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);
            File.WriteAllText(PathName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Log.Write("couldn't save prefs: " + ex.Message); }
    }
}
