using System.Diagnostics;
using Zoe.Core;
using static Zoe.App.Native;

namespace Zoe.App;

/// <summary>
/// A borderless, per-pixel-alpha, always-on-top window exactly the size of Zoe's current frame.
/// Fully transparent pixels pass clicks through to whatever is underneath; Zoe herself can be
/// clicked (wag) or picked up and thrown. Never takes focus away from her owner's work.
/// </summary>
internal sealed class PetForm : Form
{
    private readonly string _baseDir;
    private readonly string _configPath;
    private ZoeConfig _cfg;
    private SpriteSet _sprites = null!;
    private PetSim _sim = null!;
    private Desktop _desktop = null!;
    private TrayMenu _tray = null!;
    private BallForm? _ball;
    private bool _wasIdle;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTick, _lastScan = -1, _lastMood = -1, _lastTopmost;
    private List<RectI> _areas = new();
    private List<Platform> _platforms = new();
    private int _windowCount;
    private IntPtr _memDc;
    private (IntPtr Bmp, int X, int Y) _lastDrawn;
    private bool _shown;
    private string _moodReason = "";
    private FileSystemWatcher? _watcher;

    // mouse
    private bool _mouseDown, _dragging;
    private Point _downAt;
    private readonly Queue<(double T, Point P)> _trail = new();

    public static bool Diag;
    public static double ExitAfter;
    private double _lastDiag;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Mood? Override { get; set; }
    public PetSim Sim => _sim;

    public PetForm(string baseDir)
    {
        _baseDir = baseDir;
        _configPath = Path.Combine(baseDir, "zoe.json");
        _cfg = LoadConfig();
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-2000, -2000);
        Size = new Size(1, 1);
        Text = "Zoe";
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE) { m.Result = MA_NOACTIVATE; return; }
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        double dpi = DeviceDpi / 96.0;
        double scale = _cfg.Scale * dpi;
        _sprites = new SpriteSet(Path.Combine(_baseDir, "sprites"), scale, _cfg.PixelArt);
        _sim = new PetSim(_sprites.Infos, _cfg, new Random(), scale) { HalfWidth = _sprites.TypicalWidth / 2.0 };
        _desktop = new Desktop(Handle);
        _memDc = CreateCompatibleDC(IntPtr.Zero);
        if (_cfg.HideFromScreenCapture) SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE);
        if (_sprites.Infos.ContainsKey("ball")) _ball = new BallForm(_sim, _sprites, _cfg.HideFromScreenCapture);
        _tray = new TrayMenu(this, _sprites.MakeIcon());

        var home = Desktop.WorkAreas()[0];
        _sim.Spawn(home.Left + home.Width * (0.3 + Random.Shared.NextDouble() * 0.4), home.Top - _sprites.MaxHeight);

        _sim.CursorX = Cursor.Position.X;
        _sim.Greet(); // hello! (once she's landed)

        _timer.Tick += (_, _) => SafeTick();
        _timer.Start();
        WatchConfig();
        Log.Write($"started, scale {scale:0.00}, {_sprites.Infos.Count} animations");
    }

    private void SafeTick()
    {
        try { Tick(); }
        catch (Exception ex) { Log.Write("tick error: " + ex); }
    }

    private void Tick()
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = now - _lastTick;
        _lastTick = now;

        if (now - _lastScan >= 0.1)
        {
            _lastScan = now;
            _areas = Desktop.WorkAreas();
            var windows = _desktop.Windows(false);
            _windowCount = windows.Count;
            _platforms = PlatformBuilder.Build(new World(_areas, windows), _sprites.MaxHeight * 0.8, _sprites.MaxHeight * 0.6);
        }
        if (now - _lastMood >= 1.0)
        {
            _lastMood = now;
            UpdateMood();
        }
        if (now - _lastTopmost >= 3.0 && _shown)
        {
            _lastTopmost = now;
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            _ball?.KeepOnTop();
        }

        _sim.CursorX = Cursor.Position.X;
        _sim.Tick(dt, _platforms, _areas);
        Render();
        _ball?.Render();

        if (Diag && now - _lastDiag >= 1.0)
        {
            _lastDiag = now;
            Log.Write($"diag t={now:0} mood={_sim.Mood}({_moodReason}) state={_sim.State} anim={_sim.Anim}#{_sim.Frame} " +
                      $"pos=({_sim.X:0},{_sim.Y:0}) facingRight={_sim.FacingRight} on={_sim.Support?.Kind.ToString() ?? "air"} " +
                      $"windows={_windowCount} platforms={_platforms.Count}({string.Join(";", _platforms.Where(p => p.Kind == PlatformKind.Window).Select(p => $"{p.X1:0}-{p.X2:0}@{p.Y:0}"))}) " +
                      $"areas={string.Join(";", _areas.Select(a => $"{a.Left},{a.Top},{a.Right},{a.Bottom}"))} shown={_shown} " +
                      $"drawn={(GetWindowRect(Handle, out var r) ? $"{r.Left},{r.Top},{r.Right - r.Left}x{r.Bottom - r.Top}" : "?")}");
        }
        if (ExitAfter > 0 && now >= ExitAfter) { Log.Write("diag exit"); Close(); }
    }

    private void UpdateMood()
    {
        Mood mood = Mood.Normal;
        string why = "normal";
        if (Override is { } o) { mood = o; why = "tray menu"; }
        else
        {
            if (_cfg.HideWhenFullscreen && Desktop.FullscreenBusy()) { mood = Mood.Hidden; why = "full screen"; }
            else
            {
                bool needAll = _cfg.Rules.Any(r => r.Scope.Equals("any", StringComparison.OrdinalIgnoreCase));
                var hit = RuleEngine.Evaluate(_cfg.Rules, _desktop.Foreground(),
                    needAll ? _desktop.Windows(true) : Array.Empty<WindowInfo>());
                if (hit is { } h) { mood = h.Mood; why = "rule " + h.Rule; }
                else MaybeBark(_desktop.Foreground());
            }
            if (mood == Mood.Normal && _cfg.IdleDozeMinutes > 0 && IdleSeconds() > _cfg.IdleDozeMinutes * 60)
            { mood = Mood.Doze; why = "idle"; }
        }

        if (mood != _sim.Mood || why != _moodReason)
        {
            if (mood != _sim.Mood) Log.Write($"mood {_sim.Mood} -> {mood} ({why})");
            // her owner's back at her desk after being away: go and say hello.
            if (_wasIdle && mood == Mood.Normal) _sim.Greet();
            _wasIdle = why == "idle";
            _sim.Mood = mood;
            _moodReason = why;
        }
    }

    /// <summary>Horses and dogs on the telly: if the window her owner's looking at mentions one, Zoe barks at it.</summary>
    private void MaybeBark(WindowInfo? fg)
    {
        if (fg is null || _cfg.BarkAt.Count == 0) return;
        foreach (var word in _cfg.BarkAt)
        {
            if (word.Length == 0) continue;
            int i = fg.Title.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            for (; i >= 0; i = fg.Title.IndexOf(word, i + 1, StringComparison.OrdinalIgnoreCase))
            {
                bool before = i == 0 || !char.IsLetter(fg.Title[i - 1]);
                int end = i + word.Length;
                bool after = end >= fg.Title.Length || !char.IsLetter(fg.Title[end]);
                if (before && after) { _sim.BarkAt((fg.Bounds.Left + fg.Bounds.Right) / 2.0); return; }
            }
        }
    }

    private void Render()
    {
        if (!_sim.Visible)
        {
            if (_shown) { Hide(); _shown = false; }
            return;
        }
        if (!_shown) { Show(); _shown = true; _lastDrawn = default; }

        var f = _sprites.Get(_sim.Anim, _sim.Frame, flip: !_sim.FacingRight);
        int x = (int)Math.Round(_sim.X - f.Width / 2.0);
        int y = (int)Math.Round(_sim.Y - f.Height + _sprites.FootOffset);
        if (_lastDrawn == (f.HBitmap, x, y)) return;
        _lastDrawn = (f.HBitmap, x, y);

        var screenDc = GetDC(IntPtr.Zero);
        var old = SelectObject(_memDc, f.HBitmap);
        try
        {
            var dst = new POINT { X = x, Y = y };
            var size = new SIZE { CX = f.Width, CY = f.Height };
            var src = new POINT();
            var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
            UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, _memDc, ref src, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(_memDc, old);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    // ---------- mouse: click to wag, drag to carry, release to throw ----------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _mouseDown = true; _dragging = false;
        _downAt = Cursor.Position;
        _trail.Clear();
    }

    // Rubbing the mouse back and forth over her (no button) is a belly rub.
    private int _rubDir, _rubTurns, _rubX;
    private double _rubStart;

    private void TrackRub(Point p)
    {
        double t = _clock.Elapsed.TotalSeconds;
        if (t - _rubStart > 1.5) { _rubStart = t; _rubTurns = 0; _rubDir = 0; _rubX = p.X; }
        int dx = p.X - _rubX;
        if (Math.Abs(dx) < 6) return;
        int dir = Math.Sign(dx);
        _rubX = p.X;
        if (_rubDir != 0 && dir != _rubDir) _rubTurns++;
        _rubDir = dir;
        if (_rubTurns >= 3) { _sim.Pet(); _rubTurns = 2; _rubStart = t; }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = Cursor.Position;
        if (!_mouseDown) { TrackRub(p); return; }
        if (!_dragging && (Math.Abs(p.X - _downAt.X) > 4 || Math.Abs(p.Y - _downAt.Y) > 4))
        {
            _dragging = true;
            _sim.BeginDrag(_downAt.X, _downAt.Y);
        }
        if (_dragging)
        {
            _sim.DragTo(p.X, p.Y);
            double t = _clock.Elapsed.TotalSeconds;
            _trail.Enqueue((t, p));
            while (_trail.Count > 0 && t - _trail.Peek().T > 0.1) _trail.Dequeue();
            Render();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_mouseDown) return;
        _mouseDown = false;
        if (_dragging)
        {
            double vx = 0, vy = 0;
            if (_trail.Count >= 2)
            {
                var a = _trail.Peek(); var b = _trail.Last();
                double dt = Math.Max(b.T - a.T, 0.016);
                vx = (b.P.X - a.P.X) / dt; vy = (b.P.Y - a.P.Y) / dt;
            }
            _sim.EndDrag(vx, vy);
        }
        else _sim.Poke();
        _dragging = false;
    }

    // ---------- tray actions ----------

    public void ComeHere()
    {
        var c = Cursor.Position;
        var area = Desktop.WorkAreas().FirstOrDefault(a => a.ContainsX(c.X));
        if (area == default) area = Desktop.WorkAreas()[0];
        _sim.Spawn(c.X, area.Top - _sprites.MaxHeight);
    }

    public void ThrowBall()
    {
        if (_sim.Mood != Mood.Normal) return;
        _sim.DropBallNearby();
    }

    public void ReloadConfig()
    {
        var n = LoadConfig();
        // PetSim holds a reference to _cfg, so copy the live-tunable values across.
        _cfg.Rules = n.Rules; _cfg.BarkAt = n.BarkAt; _cfg.NapCorner = n.NapCorner; _cfg.IdleDozeMinutes = n.IdleDozeMinutes;
        _cfg.HideWhenFullscreen = n.HideWhenFullscreen; _cfg.Energy = n.Energy;
        _cfg.WalkSpeed = n.WalkSpeed; _cfg.RunSpeed = n.RunSpeed;
        if (n.HideFromScreenCapture != _cfg.HideFromScreenCapture)
        {
            _cfg.HideFromScreenCapture = n.HideFromScreenCapture;
            SetWindowDisplayAffinity(Handle, n.HideFromScreenCapture ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
            _ball?.SetCaptureHidden(n.HideFromScreenCapture);
        }
        _lastMood = -1;
        Log.Write($"config reloaded, {_cfg.Rules.Count} rules");
    }

    private ZoeConfig LoadConfig()
    {
        try { return File.Exists(_configPath) ? ZoeConfig.Parse(File.ReadAllText(_configPath)) : new ZoeConfig(); }
        catch (Exception ex) { Log.Write("bad zoe.json, using defaults: " + ex.Message); return _cfg ?? new ZoeConfig(); }
    }

    private void WatchConfig()
    {
        _watcher = new FileSystemWatcher(_baseDir, "zoe.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size };
        var debounce = new System.Windows.Forms.Timer { Interval = 500 };
        debounce.Tick += (_, _) => { debounce.Stop(); ReloadConfig(); };
        _watcher.Changed += (_, _) => BeginInvoke(() => { debounce.Stop(); debounce.Start(); });
        _watcher.EnableRaisingEvents = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _watcher?.Dispose();
            _tray?.Dispose();
            _ball?.Dispose();
            _sprites?.Dispose();
            if (_memDc != IntPtr.Zero) DeleteDC(_memDc);
        }
        base.Dispose(disposing);
    }
}
