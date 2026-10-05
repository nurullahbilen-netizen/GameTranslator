using System.Text.Json;
using System.Text.Json.Nodes;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Models;

namespace GameTranslator.Infrastructure.Extractors;

public sealed class JsonLocalizationExtractor : ILocalizationExtractor
{
    public string Name => "JSON";
    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".json", ".lang" };

    public bool CanHandle(string filePath) => SupportedExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

    public async Task<ExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var node = JsonNode.Parse(json) ?? throw new InvalidDataException("JSON parse edilemedi.");
        var result = new ExtractionResult { FilePath = filePath, Format = Name };
        Walk(node, "$", filePath, result.Entries);
        return result;
    }

    private static void Walk(JsonNode? node, string path, string filePath, List<LocalizationEntry> entries)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                    Walk(kv.Value, path + "." + kv.Key, filePath, entries);
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                    Walk(arr[i], $"{path}[{i}]", filePath, entries);
                break;
            case JsonValue value:
                if (value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                {
                    entries.Add(new LocalizationEntry
                    {
                        Id = path,
                        SourceText = text,
                        SourceFile = filePath
                    });
                }
                break;
        }
    }

    public async Task WriteAsync(string sourceFilePath, IReadOnlyCollection<LocalizationEntry> entries, string outputFilePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(sourceFilePath, cancellationToken);
        var node = JsonNode.Parse(json) ?? throw new InvalidDataException("JSON parse edilemedi.");
        foreach (var entry in entries.Where(e => !string.IsNullOrWhiteSpace(e.TranslatedText)))
            SetByPath(node, entry.Id, entry.TranslatedText!);

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(outputFilePath, node.ToJsonString(options), cancellationToken);
    }

    private static void SetByPath(JsonNode root, string path, string value)
    {
        if (!path.StartsWith("$", StringComparison.Ordinal)) return;
        var tokens = path[1..].Split('.', StringSplitOptions.RemoveEmptyEntries);
        JsonNode? current = root;
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.Contains('[')) return; // POC: arrays export tarafında bilerek sınırlı.
            if (current is not JsonObject obj) return;
            if (i == tokens.Length - 1) obj[token] = value;
            else current = obj[token];
        }
    }
}
