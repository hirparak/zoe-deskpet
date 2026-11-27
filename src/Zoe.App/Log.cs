namespace Zoe.App;

/// <summary>Tiny log at %LOCALAPPDATA%\Zoe\zoe.log. Records mood changes by rule name, never window titles.</summary>
internal static class Log
{
    private static readonly string PathName =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zoe", "zoe.log");

    public static void Write(string msg)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);
            var fi = new FileInfo(PathName);
            if (fi.Exists && fi.Length > 512 * 1024) File.Move(PathName, PathName + ".old", overwrite: true);
            File.AppendAllText(PathName, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
        }
        catch { /* logging must never take Zoe down */ }
    }
}
