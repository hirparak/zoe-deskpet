namespace Zoe.Core;

public static class PlatformBuilder
{
    /// <param name="headroom">Space needed above a window edge for Zoe to fit (her sprite height).</param>
    /// <param name="minWidth">Visible segments narrower than this are dropped.</param>
    public static List<Platform> Build(World world, double headroom, double minWidth)
    {
        var result = new List<Platform>();

        foreach (var wa in world.WorkAreas)
            result.Add(new Platform(wa.Left, wa.Right, wa.Bottom, PlatformKind.Floor, 0));

        for (int i = 0; i < world.Windows.Count; i++)
        {
            var w = world.Windows[i].Bounds;
            int y = w.Top;

            // The edge must sit on a monitor with room for Zoe above it
            // (rules out maximised windows whose top is the screen top).
            var wa = world.WorkAreas.FirstOrDefault(a =>
                y - headroom >= a.Top && y < a.Bottom && w.Right > a.Left && w.Left < a.Right);
            if (wa == default) continue;

            var segments = new List<(double A, double B)> { (Math.Max(w.Left, wa.Left), Math.Min(w.Right, wa.Right)) };

            // Remove the parts of the edge hidden behind windows higher in z-order.
            for (int j = 0; j < i && segments.Count > 0; j++)
            {
                var c = world.Windows[j].Bounds;
                if (c.Top <= y && c.Bottom > y)
                    segments = Subtract(segments, c.Left, c.Right);
            }

            foreach (var (a, b) in segments)
                if (b - a >= minWidth)
                    result.Add(new Platform(a, b, y, PlatformKind.Window, world.Windows[i].Id));
        }

        return result;
    }

    private static List<(double, double)> Subtract(List<(double A, double B)> segs, double cutA, double cutB)
    {
        var output = new List<(double, double)>();
        foreach (var (a, b) in segs)
        {
            if (cutB <= a || cutA >= b) { output.Add((a, b)); continue; }
            if (cutA > a) output.Add((a, cutA));
            if (cutB < b) output.Add((cutB, b));
        }
        return output;
    }
}
