using Zoe.Core;

namespace Zoe.App;

/// <summary>her owner's controls: a little Zoe in the notification area.</summary>
internal sealed class TrayMenu : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _nap, _away;
    private readonly PetForm _form;

    public TrayMenu(PetForm form, Icon icon)
    {
        _form = form;
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Zoe 🐾") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Come here, Zoe!", null, (_, _) => { SetOverride(null); _form.ComeHere(); });
        menu.Items.Add("Throw the ball", null, (_, _) => { SetOverride(null); _form.ThrowBall(); });
        _nap = new ToolStripMenuItem("Nap time", null, (_, _) => SetOverride(_nap!.Checked ? null : Mood.Nap));
        _away = new ToolStripMenuItem("Go to bed (hide)", null, (_, _) => SetOverride(_away!.Checked ? null : Mood.Hidden));
        menu.Items.Add(_nap);
        menu.Items.Add(_away);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Goodbye for today", null, (_, _) => Application.Exit());

        _icon = new NotifyIcon { Icon = icon, Text = "Zoe", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => { SetOverride(null); _form.ComeHere(); };
    }

    private void SetOverride(Mood? m)
    {
        _form.Override = m;
        _nap.Checked = m == Mood.Nap;
        _away.Checked = m == Mood.Hidden;
        Log.Write($"tray override: {m?.ToString() ?? "none"}");
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
