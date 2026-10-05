using System.Text;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Models;

namespace GameTranslator.Infrastructure.Extractors;

public sealed class CsvLocalizationExtractor : ILocalizationExtractor
{
    public string Name => "CSV";
    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".csv" };
    public bool CanHandle(string filePath) => string.Equals(Path.GetExtension(filePath), ".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<ExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var result = new ExtractionResult { FilePath = filePath, Format = Name };
        for (var i = 0; i < lines.Length; i++)
        {
            var cols = SplitCsvLine(lines[i]);
            if (cols.Count < 2) continue;
            if (i == 0 && cols[0].Contains("id", StringComparison.OrdinalIgnoreCase)) continue;
            result.Entries.Add(new LocalizationEntry
            {
                Id = cols[0],
                SourceText = cols[1],
                SourceFile = filePath
            });
        }
        return result;
    }

    public async Task WriteAsync(string sourceFilePath, IReadOnlyCollection<LocalizationEntry> entries, string outputFilePath, CancellationToken cancellationToken = default)
    {
        var lines = await File.ReadAllLinesAsync(sourceFilePath, cancellationToken);
        var map = entries.Where(e => !string.IsNullOrWhiteSpace(e.TranslatedText)).ToDictionary(e => e.Id, e => e.TranslatedText!);
        var output = new List<string>(lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var cols = SplitCsvLine(lines[i]);
            if (cols.Count >= 2 && map.TryGetValue(cols[0], out var translated)) cols[1] = translated;
            output.Add(string.Join(',', cols.Select(Escape)));
        }

        await File.WriteAllLinesAsync(outputFilePath, output, Encoding.UTF8, cancellationToken);
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return result;
    }

    private static string Escape(string value) => value.Contains(',') || value.Contains('"') || value.Contains('\n')
        ? '"' + value.Replace("\"", "\"\"") + '"'
        : value;
}
