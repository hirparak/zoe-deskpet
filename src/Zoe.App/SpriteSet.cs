using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;
using Zoe.Core;

namespace Zoe.App;

/// <summary>
/// Loads sprites/&lt;animation&gt;/*.png (frames in filename order, drawn facing RIGHT, feet at the
/// bottom-centre) plus sprites/sprites.json for per-animation fps/loop.
/// Frames are cached as premultiplied GDI bitmaps ready for UpdateLayeredWindow.
/// </summary>
internal sealed class SpriteSet : IDisposable
{
    internal sealed class Frame(IntPtr hbitmap, int width, int height)
    {
        public IntPtr HBitmap { get; } = hbitmap;
        public int Width { get; } = width;
        public int Height { get; } = height;
    }

    private sealed class Meta
    {
        public int FootOffset { get; set; }
        public Dictionary<string, AnimMeta> Animations { get; set; } = new();
    }

    private sealed class AnimMeta
    {
        public double Fps { get; set; } = 8;
        public bool Loop { get; set; } = true;
    }

    private readonly Dictionary<string, List<Bitmap>> _source = new();
    private readonly Dictionary<(string, int, bool), Frame> _cache = new();
    private readonly double _scale;
    private readonly bool _pixelArt;

    public Dictionary<string, AnimInfo> Infos { get; } = new();
    public int FootOffset { get; }
    public int MaxHeight { get; }
    /// <summary>Width of her standing/walking frame, scaled.</summary>
    public int TypicalWidth { get; }

    public SpriteSet(string dir, double scale, bool pixelArt)
    {
        _scale = scale;
        _pixelArt = pixelArt;
        var metaPath = Path.Combine(dir, "sprites.json");
        var meta = File.Exists(metaPath)
            ? JsonSerializer.Deserialize<Meta>(File.ReadAllText(metaPath), ZoeConfig.JsonOptions) ?? new Meta()
            : new Meta();

        foreach (var sub in Directory.GetDirectories(dir))
        {
            var files = Directory.GetFiles(sub, "*.png").OrderBy(f => f, StringComparer.Ordinal).ToList();
            if (files.Count == 0) continue;
            string name = Path.GetFileName(sub).ToLowerInvariant();
            _source[name] = files.Select(f => { using var b = new Bitmap(f); return new Bitmap(b); }).ToList();
            var m = meta.Animations.GetValueOrDefault(name) ?? new AnimMeta();
            Infos[name] = new AnimInfo(files.Count, m.Fps, m.Loop);
        }
        if (!Infos.ContainsKey("sit"))
            throw new InvalidOperationException($"No sprites/sit/*.png found in {dir}");

        FootOffset = (int)Math.Round(meta.FootOffset * scale);
        MaxHeight = (int)Math.Round(_source.Values.SelectMany(v => v).Max(b => b.Height) * scale);
        var typical = _source.GetValueOrDefault("walk") ?? _source["sit"];
        TypicalWidth = (int)Math.Round(typical[0].Width * scale);
    }

    public Frame Get(string anim, int index, bool flip)
    {
        var key = (anim, index, flip);
        if (_cache.TryGetValue(key, out var f)) return f;

        var src = _source[anim][index];
        int w = Math.Max(1, (int)Math.Round(src.Width * _scale)), h = Math.Max(1, (int)Math.Round(src.Height * _scale));
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = _pixelArt ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingMode = CompositingMode.SourceCopy;
            if (flip) { g.TranslateTransform(w, 0); g.ScaleTransform(-1, 1); }
            g.DrawImage(src, 0, 0, w, h);
        }
        // GetHbitmap with a transparent background yields a premultiplied 32bpp DIB.
        f = new Frame(bmp.GetHbitmap(Color.FromArgb(0)), w, h);
        _cache[key] = f;
        return f;
    }

    public Icon MakeIcon()
    {
        var src = _source["sit"][0];
        using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            double k = Math.Min(32.0 / src.Width, 32.0 / src.Height);
            int w = (int)(src.Width * k), h = (int)(src.Height * k);
            g.DrawImage(src, (32 - w) / 2, 32 - h, w, h);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        foreach (var f in _cache.Values) Native.DeleteObject(f.HBitmap);
        _cache.Clear();
        foreach (var b in _source.Values.SelectMany(v => v)) b.Dispose();
    }
}
