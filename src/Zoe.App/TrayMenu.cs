using Microsoft.Win32;
using Zoe.Core;

namespace Zoe.App;

/// <summary>her owner's controls: a little Zoe in the notification area.</summary>
internal sealed class TrayMenu : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _auto, _playful, _nap, _away, _autostart;
    private readonly (ToolStripMenuItem Item, double Value)[] _energy;
    private readonly PetForm _form;
    private readonly System.Windows.Forms.Timer _promote;

    public TrayMenu(PetForm form, Icon icon)
    {
        _form = form;
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Zoe 🐾") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Come here, Zoe!", null, (_, _) => { WakeForPlay(); _form.ComeHere(); });
        menu.Items.Add("Throw the ball", null, (_, _) => { WakeForPlay(); _form.ThrowBall(); });
        menu.Items.Add(new ToolStripSeparator());

        _auto = new ToolStripMenuItem("Up to her (naps while you work)", null, (_, _) => SetOverride(null));
        _playful = new ToolStripMenuItem("Stay up and play", null, (_, _) => SetOverride(Mood.Normal));
        _nap = new ToolStripMenuItem("Nap time", null, (_, _) => SetOverride(Mood.Nap));
        _away = new ToolStripMenuItem("Out of sight", null, (_, _) => SetOverride(Mood.Hidden));
        var mood = new ToolStripMenuItem("Mood");
        mood.DropDownItems.AddRange(new ToolStripItem[] { _auto, _playful, _nap, _away });
        menu.Items.Add(mood);

        _energy = new[] { ("Lazy day", 0.5), ("Her usual self", 1.0), ("Full of beans", 2.0) }
            .Select(e => (new ToolStripMenuItem(e.Item1, null, (_, _) => SetEnergy(e.Item2)), e.Item2)).ToArray();
        var energy = new ToolStripMenuItem("Energy");
        energy.DropDownItems.AddRange(_energy.Select(e => (ToolStripItem)e.Item).ToArray());
        menu.Items.Add(energy);

        menu.Items.Add(new ToolStripSeparator());
        _autostart = new ToolStripMenuItem("Start when I sign in", null, (_, _) => ToggleAutostart()) { Checked = _form.Prefs.Autostart };
        menu.Items.Add(_autostart);
        menu.Items.Add("Turn Zoe off", null, (_, _) => Application.Exit());

        _icon = new NotifyIcon { Icon = icon, Text = "Zoe", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => { WakeForPlay(); _form.ComeHere(); };
        SetOverride(null, log: false);
        ShowEnergy();

        // Windows 11 tucks new icons into the overflow arrow. Ask for a place on the taskbar itself, once Explorer
        // has registered the icon; if her owner later hides it herself, that choice is left alone.
        _promote = new System.Windows.Forms.Timer { Interval = 4000 };
        _promote.Tick += (_, _) => { _promote.Stop(); if (Promote()) { _icon.Visible = false; _icon.Visible = true; } };
        _promote.Start();
    }

    /// <summary>Asking her to play ends a nap or hiding, but leaves "stay up and play" as it is.</summary>
    private void WakeForPlay() { if (_form.Override is Mood.Nap or Mood.Hidden) SetOverride(null); }

    private void SetOverride(Mood? m, bool log = true)
    {
        _form.Override = m;
        _auto.Checked = m is null;
        _playful.Checked = m == Mood.Normal;
        _nap.Checked = m == Mood.Nap;
        _away.Checked = m == Mood.Hidden;
        if (log) Log.Write($"tray mood: {m?.ToString() ?? "auto"}");
    }

    private void SetEnergy(double e)
    {
        _form.Prefs.Energy = e;
        _form.Prefs.Save();
        _form.ApplyPrefs();
        ShowEnergy();
        Log.Write($"tray energy: {e:0.0}");
    }

    private void ShowEnergy()
    {
        foreach (var (item, value) in _energy) item.Checked = Math.Abs(_form.Energy - value) < 0.01;
    }

    private void ToggleAutostart()
    {
        _form.Prefs.Autostart = _autostart.Checked = !_autostart.Checked;
        _form.Prefs.Save();
        Log.Write($"tray autostart: {_form.Prefs.Autostart}");
    }

    /// <summary>Mark our notification icon as "always show". True if anything changed.</summary>
    private static bool Promote()
    {
        try
        {
            string exe = Environment.ProcessPath ?? "";
            using var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            if (root is null) return false;   // Windows 10: no such list, icons are managed differently
            bool changed = false;
            foreach (string name in root.GetSubKeyNames())
            {
                using var k = root.OpenSubKey(name, writable: true);
                // Explorer stores the path with a folder id in place of "C:\Program Files", so compare the tail.
                if (k?.GetValue("ExecutablePath") is not string path || !Tail(path).Equals(Tail(exe), StringComparison.OrdinalIgnoreCase)) continue;
                if (k.GetValue("IsPromoted") is null) { k.SetValue("IsPromoted", 1, RegistryValueKind.DWord); changed = true; }
            }
            if (changed) Log.Write("tray icon set to always show");
            return changed;
        }
        catch (Exception ex) { Log.Write("couldn't pin tray icon: " + ex.Message); return false; }
    }

    /// <summary>The last two parts of a path: "Zoe\Zoe.exe".</summary>
    private static string Tail(string path)
    {
        var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('\\', parts.Skip(Math.Max(0, parts.Length - 2)));
    }

    public void Dispose()
    {
        _promote.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
    }
}
