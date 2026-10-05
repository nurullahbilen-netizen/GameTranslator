using System.Text;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Models;

namespace GameTranslator.Infrastructure.Extractors;

public sealed class PoLocalizationExtractor : ILocalizationExtractor
{
    public string Name => "PO";
    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".po" };
    public bool CanHandle(string filePath) => string.Equals(Path.GetExtension(filePath), ".po", StringComparison.OrdinalIgnoreCase);

    public async Task<ExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var result = new ExtractionResult { FilePath = filePath, Format = Name };
        string? msgctxt = null, msgid = null, msgstr = null;

        void Flush()
        {
            if (!string.IsNullOrWhiteSpace(msgid))
            {
                result.Entries.Add(new LocalizationEntry
                {
                    Id = msgctxt ?? msgid,
                    SourceText = msgid,
                    TranslatedText = string.IsNullOrWhiteSpace(msgstr) ? null : msgstr,
                    SourceFile = filePath,
                    Context = msgctxt
                });
            }
            msgctxt = msgid = msgstr = null;
        }

        foreach (var raw in lines.Append(string.Empty))
        {
            var line = raw.Trim();
            if (line.Length == 0) { Flush(); continue; }
            if (line.StartsWith("msgctxt ")) msgctxt = Unquote(line[8..]);
            else if (line.StartsWith("msgid ")) msgid = Unquote(line[6..]);
            else if (line.StartsWith("msgstr ")) msgstr = Unquote(line[7..]);
        }
        return result;
    }

    public async Task WriteAsync(string sourceFilePath, IReadOnlyCollection<LocalizationEntry> entries, string outputFilePath, CancellationToken cancellationToken = default)
    {
        var map = entries.Where(e => !string.IsNullOrWhiteSpace(e.TranslatedText))
            .ToDictionary(e => e.Id, e => e.TranslatedText!, StringComparer.Ordinal);
        var lines = await File.ReadAllLinesAsync(sourceFilePath, cancellationToken);
        var output = new List<string>();
        string? currentContext = null, currentId = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("msgctxt ")) currentContext = Unquote(line[8..]);
            if (line.StartsWith("msgid ")) currentId = Unquote(line[6..]);
            if (line.StartsWith("msgstr ") && currentId is not null)
            {
                var key = currentContext ?? currentId;
                if (map.TryGetValue(key, out var translated))
                {
                    output.Add("msgstr \"" + Escape(translated) + "\"");
                    continue;
                }
            }
            output.Add(raw);
            if (line.Length == 0) { currentContext = null; currentId = null; }
        }

        await File.WriteAllLinesAsync(outputFilePath, output, new UTF8Encoding(false), cancellationToken);
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
        return value.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
}
