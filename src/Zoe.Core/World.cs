namespace Zoe.Core;

/// <summary>Integer screen rectangle in physical pixels. Right/Bottom are exclusive.</summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool ContainsX(double x) => x >= Left && x < Right;
}

/// <summary>A visible top-level window. Lists of these are always in z-order, topmost first.</summary>
/// <param name="Url">Address-bar text when the window is a browser and its address was read; otherwise null.</param>
public sealed record WindowInfo(long Id, RectI Bounds, string Title = "", string Process = "", string? Url = null);

public enum PlatformKind { Floor, Window }

/// <summary>A horizontal surface Zoe can stand on: the taskbar edge or a window's top edge.</summary>
public readonly record struct Platform(double X1, double X2, double Y, PlatformKind Kind, long WindowId)
{
    public bool Spans(double x) => x >= X1 && x <= X2;
    public double Width => X2 - X1;
}

/// <summary>Snapshot of the desktop. WorkAreas[0] is the primary monitor.</summary>
public sealed record World(IReadOnlyList<RectI> WorkAreas, IReadOnlyList<WindowInfo> Windows);
