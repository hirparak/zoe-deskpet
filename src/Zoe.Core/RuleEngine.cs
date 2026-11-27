namespace Zoe.Core;

public static class RuleEngine
{
    /// <summary>
    /// Returns the strongest mood demanded by any rule (Hidden &gt; Nap &gt; Doze), or null if none match.
    /// </summary>
    public static (Mood Mood, string Rule)? Evaluate(IEnumerable<Rule> rules, WindowInfo? foreground, IReadOnlyList<WindowInfo> visible)
    {
        (Mood Mood, string Rule)? best = null;
        foreach (var rule in rules)
        {
            bool hit = rule.Scope.Equals("any", StringComparison.OrdinalIgnoreCase)
                ? visible.Any(w => Matches(rule, w))
                : foreground is not null && Matches(rule, foreground);
            if (hit && (best is null || Rank(rule.Mood) > Rank(best.Value.Mood)))
                best = (rule.Mood, rule.Name);
        }
        return best;
    }

    public static bool Matches(Rule rule, WindowInfo w)
    {
        if (rule.Process.Count == 0 && rule.Title.Count == 0 && rule.Url.Count == 0) return false;
        bool procOk = rule.Process.Count == 0 ||
            rule.Process.Any(p => string.Equals(StripExe(p), StripExe(w.Process), StringComparison.OrdinalIgnoreCase));
        // Title and url are alternatives: web apps often don't put their name in the tab title.
        bool textOk = (rule.Title.Count == 0 && rule.Url.Count == 0) ||
            rule.Title.Any(t => w.Title.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
            (w.Url is { } u && rule.Url.Any(x => u.Contains(x, StringComparison.OrdinalIgnoreCase)));
        return procOk && textOk;
    }

    public static int Rank(Mood m) => m switch { Mood.Hidden => 3, Mood.Nap => 2, Mood.Doze => 1, _ => 0 };

    private static string StripExe(string s) =>
        s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s;
}
