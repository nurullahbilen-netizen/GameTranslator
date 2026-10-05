using System.Xml.Linq;
using GameTranslator.Core.Models;

namespace GameTranslator.Core.Extraction;

public sealed class XmlLocalizationHandler : ILocalizationHandler
{
    public string Name => "XML";
    public bool CanHandle(string path) => path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<TextEntry> Extract(string path) =>
        XDocument.Load(path).Descendants().Where(e => !e.HasElements && HandlerUtil.IsText(e.Value))
            .Select(e => new TextEntry { File = path, Key = KeyOf(e), Source = e.Value.Trim() }).ToList();

    public void Write(string sourcePath, string destPath, IEnumerable<TextEntry> entries)
    {
        var map = HandlerUtil.Map(entries);
        var doc = XDocument.Load(sourcePath);
        foreach (var e in doc.Descendants().Where(e => !e.HasElements).ToList())
            if (map.TryGetValue(KeyOf(e), out var t)) e.Value = t;
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        doc.Save(destPath);
    }

    private static string KeyOf(XElement e) => string.Join("/", e.AncestorsAndSelf().Reverse()
        .Select(a => $"{a.Name.LocalName}[{a.ElementsBeforeSelf(a.Name).Count()}]"));
}
