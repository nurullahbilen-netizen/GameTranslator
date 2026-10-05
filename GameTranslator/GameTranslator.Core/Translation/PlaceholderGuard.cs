using System.Text.RegularExpressions;

namespace GameTranslator.Core.Translation;

/// <summary>{0}, {name}, %s, &lt;color&gt; ve \n gibi öğeleri çeviriden önce §N§ ile korur, sonra geri koyar.</summary>
public static partial class PlaceholderGuard
{
    [GeneratedRegex(@"\{[^{}]*\}|%[-+0 #]*\d*(?:\.\d+)?[sdifx]|<[^<>]+>|\\[nrt]|\r|\n")]
    private static partial Regex Pattern();

    public static (string Text, List<string> Tokens) Protect(string s)
    {
        var tokens = new List<string>();
        var text = Pattern().Replace(s, m => { tokens.Add(m.Value); return $"§{tokens.Count - 1}§"; });
        return (text, tokens);
    }

    /// <summary>Bir token kaybolduysa null döner (çeviri güvenilmez).</summary>
    public static string? Restore(string s, List<string> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            var rx = new Regex($@"§\s*{i}\s*§");
            if (!rx.IsMatch(s)) return null;
            s = rx.Replace(s, tokens[i].Replace("$", "$$"));
        }
        return s;
    }
}
