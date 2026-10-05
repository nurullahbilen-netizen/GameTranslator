namespace GameTranslator.Core.Text;

public sealed class ProtectedText
{
    private readonly IReadOnlyDictionary<string, string> _tokens;

    internal ProtectedText(string text, IReadOnlyDictionary<string, string> tokens)
    {
        Text = text;
        _tokens = tokens;
    }

    public string Text { get; }
    public IReadOnlyDictionary<string, string> Tokens => _tokens;

    public string Restore(string translatedText)
    {
        ArgumentNullException.ThrowIfNull(translatedText);

        var result = translatedText;
        foreach (var pair in _tokens.OrderByDescending(x => x.Key.Length))
            result = result.Replace(pair.Key, pair.Value, StringComparison.Ordinal);

        return result;
    }

    public bool ContainsAllTokens(string candidate)
        => _tokens.Keys.All(token => candidate.Contains(token, StringComparison.Ordinal));
}
