using System.Text.RegularExpressions;

namespace GameTranslator.Core.Text;

/// <summary>
/// Protects formatting/runtime tokens before text is sent to a translation engine.
/// Examples: {playerName}, {0:N0}, %d, %1$s, \\n, &lt;color=...&gt;, [/tag].
/// </summary>
public sealed partial class PlaceholderProtector
{
    // Order matters: more specific patterns should appear before broad tag patterns.
    [GeneratedRegex(
        @"(\{[A-Za-z_][A-Za-z0-9_.-]*(?:[^{}]*)?\}|\{\d+(?:[^{}]*)?\}|\{\{|\}\}|\\[nrt0\\]|%(?:\d+\$)?[-+#0 ' ]*\d*(?:\.\d+)?[diuoxXfFeEgGaAcspn%]|<\/?[A-Za-z][^<>]*>|\[[A-Za-z][^\[\]]*\]|\[\/[A-Za-z][^\[\]]*\])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    public ProtectedText Protect(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;

        string Evaluator(Match match)
        {
            // %% is a literal percent in printf-style strings; preserving it is safest.
            string token;
            do
            {
                token = $"__GT_PH_{index++:D4}__";
            }
            while (source.Contains(token, StringComparison.Ordinal));

            tokens[token] = match.Value;
            return token;
        }

        var protectedText = PlaceholderRegex().Replace(source, Evaluator);
        return new ProtectedText(protectedText, tokens);
    }

    public string RestoreAndValidate(ProtectedText protectedText, string translatedText)
    {
        ArgumentNullException.ThrowIfNull(protectedText);
        ArgumentNullException.ThrowIfNull(translatedText);

        if (!protectedText.ContainsAllTokens(translatedText))
        {
            var missing = protectedText.Tokens.Keys
                .Where(token => !translatedText.Contains(token, StringComparison.Ordinal))
                .ToArray();

            throw new InvalidOperationException(
                $"Translation engine changed or removed protected placeholder(s): {string.Join(", ", missing)}");
        }

        return protectedText.Restore(translatedText);
    }
}
