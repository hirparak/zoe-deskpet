using System.Diagnostics;
using Zoe.Core;
using static Zoe.App.Native;

namespace Zoe.App;

/// <summary>Reads the live desktop: monitors, visible windows in z-order, and the foreground window.</summary>
internal sealed class Desktop
{
    private const uint GW_OWNER = 4;
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland",
    };

    private readonly IntPtr _self;
    private readonly Dictionary<uint, string> _procNames = new();

    public Desktop(IntPtr self) => _self = self;

    public static List<RectI> WorkAreas()
    {
        var screens = Screen.AllScreens.OrderByDescending(s => s.Primary).ToList();
        return screens.Select(s => new RectI(s.WorkingArea.Left, s.WorkingArea.Top, s.WorkingArea.Right, s.WorkingArea.Bottom)).ToList();
    }

    /// <summary>Visible top-level app windows, topmost first.</summary>
    public List<WindowInfo> Windows(bool withDetails)
    {
        var list = new List<WindowInfo>();
        EnumWindows((h, _) =>
        {
            if (h == _self || !IsWindowVisible(h) || IsIconic(h)) return true;
            if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            int ex = GetWindowLong(h, GWL_EXSTYLE);
            if ((ex & WS_EX_TOOLWINDOW) != 0 && (ex & WS_EX_APPWINDOW) == 0) return true;
            if (IgnoredClasses.Contains(ClassName(h))) return true;
            if (GetWindowTextLength(h) == 0 && GetWindow(h, GW_OWNER) == IntPtr.Zero && (ex & WS_EX_APPWINDOW) == 0) return true;

            // Extended frame bounds exclude the invisible resize border Windows 10/11 adds.
            if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) != 0)
                GetWindowRect(h, out r);
            if (r.Right - r.Left < 120 || r.Bottom - r.Top < 60) return true;

            list.Add(withDetails
                ? Describe(h, new RectI(r.Left, r.Top, r.Right, r.Bottom))
                : new WindowInfo(h.ToInt64(), new RectI(r.Left, r.Top, r.Right, r.Bottom)));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public WindowInfo? Foreground()
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero || h == _self) return null;
        GetWindowRect(h, out var r);
        var w = Describe(h, new RectI(r.Left, r.Top, r.Right, r.Bottom));
        return BrowserUrl.IsBrowser(w.Process) ? w with { Url = _urls.Get(h, w.Title) } : w;
    }

    private readonly BrowserUrl _urls = new();

    private WindowInfo Describe(IntPtr h, RectI bounds)
    {
        GetWindowThreadProcessId(h, out uint pid);
        return new WindowInfo(h.ToInt64(), bounds, WindowText(h), ProcessName(pid));
    }

    private string ProcessName(uint pid)
    {
        if (_procNames.TryGetValue(pid, out var n)) return n;
        try { n = Process.GetProcessById((int)pid).ProcessName; }
        catch { n = ""; }
        if (_procNames.Count > 500) _procNames.Clear();
        return _procNames[pid] = n;
    }

    /// <summary>True while a full-screen app, game or PowerPoint slideshow is running.</summary>
    public static bool FullscreenBusy() =>
        SHQueryUserNotificationState(out int s) == 0 && s is 2 or 3 or 4;
}
