using System.Diagnostics;
using Zoe.Core;
using static Zoe.App.Native;

namespace Zoe.App;

/// <summary>
/// Zoe's tennis ball: its own tiny per-pixel-alpha window so it can bounce around independently of her.
/// her owner can grab it with the mouse and throw it. Like Zoe, it never takes focus.
/// </summary>
internal sealed class BallForm : Form
{
    private readonly PetSim _sim;
    private readonly SpriteSet _sprites;
    private readonly IntPtr _memDc = CreateCompatibleDC(IntPtr.Zero);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Queue<(double T, Point P)> _trail = new();
    private (IntPtr Bmp, int X, int Y) _lastDrawn;
    private bool _shown, _mouseDown;
    private readonly int _frames, _size;

    public BallForm(PetSim sim, SpriteSet sprites, bool hideFromCapture)
    {
        _sim = sim; _sprites = sprites;
        _frames = sprites.Infos["ball"].Frames;
        _size = sprites.Get("ball", 0, false).Width;
        sim.BallRadius = _size / 2.0;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-2000, -2000);
        Size = new Size(1, 1);
        Text = "Zoe's ball";
        Cursor = Cursors.Hand;
        CreateHandle();
        if (hideFromCapture) SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE);
    }

    public void SetCaptureHidden(bool hidden) => SetWindowDisplayAffinity(Handle, hidden ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);

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

    public void KeepOnTop()
    {
        if (_shown) SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void Render()
    {
        if (!_sim.BallVisible)
        {
            if (_shown) { Hide(); _shown = false; _mouseDown = false; }
            return;
        }
        if (!_shown) { Show(); _shown = true; _lastDrawn = default; }

        // One full turn per circumference rolled.
        double turns = _sim.BallRoll / (Math.PI * _size);
        int frame = (int)Math.Floor((turns - Math.Floor(turns)) * _frames) % _frames;
        var f = _sprites.Get("ball", frame, false);
        int x = (int)Math.Round(_sim.BallX - f.Width / 2.0);
        int y = (int)Math.Round(_sim.BallY - f.Height);
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

    // ---------- mouse: grab the ball, fling it ----------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _mouseDown = true;
        _trail.Clear();
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_mouseDown) return;
        var p = Cursor.Position;
        double t = _clock.Elapsed.TotalSeconds;
        _trail.Enqueue((t, p));
        while (_trail.Count > 0 && t - _trail.Peek().T > 0.1) _trail.Dequeue();
        _sim.ThrowBall(p.X, p.Y + _size / 2.0, 0, 0); // held in the hand
        Render();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_mouseDown) return;
        _mouseDown = false;
        Capture = false;
        var p = Cursor.Position;
        double vx = 0, vy = -300; // a plain click gives it a little toss
        if (_trail.Count >= 2)
        {
            var a = _trail.Peek(); var b = _trail.Last();
            double dt = Math.Max(b.T - a.T, 0.016);
            vx = (b.P.X - a.P.X) / dt; vy = (b.P.Y - a.P.Y) / dt;
        }
        else { vx = Random.Shared.Next(2) == 0 ? -350 : 350; vy = -700; p = new Point((int)_sim.BallX, (int)(_sim.BallY - _size / 2.0)); }
        _sim.ThrowBall(p.X, p.Y + _size / 2.0, vx, vy);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _memDc != IntPtr.Zero) DeleteDC(_memDc);
        base.Dispose(disposing);
    }
}
