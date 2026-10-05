using System.Xml.Linq;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Models;

namespace GameTranslator.Infrastructure.Extractors;

public sealed class XmlLocalizationExtractor : ILocalizationExtractor
{
    public string Name => "XML";
    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".xml" };
    public bool CanHandle(string filePath) => string.Equals(Path.GetExtension(filePath), ".xml", StringComparison.OrdinalIgnoreCase);

    public Task<ExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
        var result = new ExtractionResult { FilePath = filePath, Format = Name };

        foreach (var el in doc.Descendants().Where(e => !e.HasElements && !string.IsNullOrWhiteSpace(e.Value)))
        {
            var id = el.Attribute("id")?.Value ?? el.Attribute("key")?.Value ?? BuildXPath(el);
            result.Entries.Add(new LocalizationEntry
            {
                Id = id,
                SourceText = el.Value,
                SourceFile = filePath,
                Context = el.Name.LocalName
            });
        }

        return Task.FromResult(result);
    }

    public Task WriteAsync(string sourceFilePath, IReadOnlyCollection<LocalizationEntry> entries, string outputFilePath, CancellationToken cancellationToken = default)
    {
        var doc = XDocument.Load(sourceFilePath, LoadOptions.PreserveWhitespace);
        var map = entries.Where(e => !string.IsNullOrWhiteSpace(e.TranslatedText)).ToDictionary(e => e.Id, e => e.TranslatedText!);

        foreach (var el in doc.Descendants().Where(e => !e.HasElements))
        {
            var id = el.Attribute("id")?.Value ?? el.Attribute("key")?.Value ?? BuildXPath(el);
            if (map.TryGetValue(id, out var translated)) el.Value = translated;
        }

        doc.Save(outputFilePath);
        return Task.CompletedTask;
    }

    private static string BuildXPath(XElement element)
    {
        var parts = new Stack<string>();
        XElement? current = element;
        while (current is not null)
        {
            var index = current.Parent?.Elements(current.Name).TakeWhile(e => e != current).Count() + 1 ?? 1;
            parts.Push($"{current.Name.LocalName}[{index}]");
            current = current.Parent;
        }
        return "/" + string.Join('/', parts);
    }
}
