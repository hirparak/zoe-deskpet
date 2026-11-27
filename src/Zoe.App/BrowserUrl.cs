using System.Windows.Automation;

namespace Zoe.App;

/// <summary>
/// Reads the address bar of the foreground browser window via UI Automation, so rules can match web apps
/// (e.g. Acme) whose tab titles don't name them. The address is only matched against rules, never logged.
/// Lookups run off the UI thread and are cached per window + title, so Zoe's animation never waits on them.
/// </summary>
internal sealed class BrowserUrl
{
    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
        { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi" };

    public static bool IsBrowser(string process) => Browsers.Contains(process);

    private readonly object _lock = new();
    private (IntPtr Hwnd, string Title) _key;
    private string? _url;
    private DateTime _fetched;
    private bool _busy;

    public string? Get(IntPtr hwnd, string title)
    {
        lock (_lock)
        {
            bool same = _key == (hwnd, title);
            // Refresh when the tab/window changes, and every few seconds anyway (some sites keep one title).
            if ((!same || DateTime.UtcNow - _fetched > TimeSpan.FromSeconds(3)) && !_busy)
            {
                _busy = true;
                var key = (hwnd, title);
                Task.Run(() =>
                {
                    string? url = Read(hwnd);
                    lock (_lock) { _key = key; _url = url; _fetched = DateTime.UtcNow; _busy = false; }
                });
            }
            return same ? _url : null;
        }
    }

    private static readonly Condition EditCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);

    private static string? Read(IntPtr hwnd)
    {
        try
        {
            var win = AutomationElement.FromHandle(hwnd);
            // The first edit box in a browser window is its address bar.
            var edit = win?.FindFirst(TreeScope.Descendants, EditCondition);
            return edit?.GetCurrentPattern(ValuePattern.Pattern) is ValuePattern vp ? vp.Current.Value : null;
        }
        catch { return null; }
    }
}
