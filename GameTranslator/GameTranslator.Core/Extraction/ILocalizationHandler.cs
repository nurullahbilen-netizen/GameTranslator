using GameTranslator.Core.Models;

namespace GameTranslator.Core.Extraction;

/// <summary>Bir dosya formatı için okuma (Extract) ve geri yazma (Write/Injector) sözleşmesi.
/// PAK / Asset Bundle gibi formatlar için yeni bir handler eklemek yeterlidir.</summary>
public interface ILocalizationHandler
{
    string Name { get; }
    bool CanHandle(string path);
    IReadOnlyList<TextEntry> Extract(string path);
    /// <summary>sourcePath'teki orijinali okuyup çevirileri uygulayarak destPath'e yazar.</summary>
    void Write(string sourcePath, string destPath, IEnumerable<TextEntry> entries);
}

internal static class HandlerUtil
{
    public static Dictionary<string, string> Map(IEnumerable<TextEntry> entries) =>
        entries.Where(e => !string.IsNullOrWhiteSpace(e.Translation))
               .GroupBy(e => e.Key)
               .ToDictionary(g => g.Key, g => g.First().Translation!);

    public static bool IsText(string s) => s.Trim().Length > 1 && s.Any(char.IsLetter);
}
