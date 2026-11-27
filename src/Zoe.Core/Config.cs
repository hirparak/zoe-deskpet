using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zoe.Core;

public enum Mood
{
    Normal,
    /// <summary>Lie down and sleep wherever she is (her owner stepped away).</summary>
    Doze,
    /// <summary>Trot to the nap corner and sleep there.</summary>
    Nap,
    /// <summary>Not drawn at all.</summary>
    Hidden,
}

public sealed class Rule
{
    public string Name { get; set; } = "";
    /// <summary>Process names without ".exe", case-insensitive. Any one matches.</summary>
    public List<string> Process { get; set; } = new();
    /// <summary>Substrings of the window title, case-insensitive. Any one matches.</summary>
    public List<string> Title { get; set; } = new();
    /// <summary>Substrings of the browser address (active tab, foreground browser only), case-insensitive.
    /// A rule with both title and url matches on either.</summary>
    public List<string> Url { get; set; } = new();
    public Mood Mood { get; set; } = Mood.Nap;
    /// <summary>"foreground" (default): only the active window. "any": any visible window.</summary>
    public string Scope { get; set; } = "foreground";
}

public sealed class ZoeConfig
{
    public double Scale { get; set; } = 1.0;
    public bool PixelArt { get; set; } = true;
    public string NapCorner { get; set; } = "bottom-right";
    /// <summary>Exclude Zoe from screenshots and screen shares (Teams/Zoom). Windows 10 2004+.</summary>
    public bool HideFromScreenCapture { get; set; } = true;
    /// <summary>Hide during full-screen apps and PowerPoint presentations.</summary>
    public bool HideWhenFullscreen { get; set; } = true;
    /// <summary>Doze after this many minutes without keyboard/mouse input. 0 disables.</summary>
    public double IdleDozeMinutes { get; set; } = 5;
    public double WalkSpeed { get; set; } = 70;
    public double RunSpeed { get; set; } = 260;
    /// <summary>Overall liveliness multiplier: higher = more running/jumping.</summary>
    public double Energy { get; set; } = 1.0;
    /// <summary>Words in the active window's title that set her off barking (whole words, case-insensitive).</summary>
    public List<string> BarkAt { get; set; } = new() { "horse", "horses", "pony", "dog", "dogs", "puppy", "puppies" };
    public List<Rule> Rules { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true,
    };

    public static ZoeConfig Parse(string json) =>
        JsonSerializer.Deserialize<ZoeConfig>(json, JsonOptions) ?? new ZoeConfig();
}
