using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameTranslator.Core.Models;

namespace GameTranslator.Core.Extraction;

public sealed class JsonLocalizationHandler : ILocalizationHandler
{
    public string Name => "JSON";
    public bool CanHandle(string path) => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static JsonNode? Load(string path) => JsonNode.Parse(File.ReadAllText(path),
        documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

    public IReadOnlyList<TextEntry> Extract(string path)
    {
        var list = new List<TextEntry>();
        Walk(Load(path), "", (key, value) =>
        {
            if (HandlerUtil.IsText(value)) list.Add(new TextEntry { File = path, Key = key, Source = value });
            return null;
        });
        return list;
    }

    public void Write(string sourcePath, string destPath, IEnumerable<TextEntry> entries)
    {
        var map = HandlerUtil.Map(entries);
        var root = Load(sourcePath);
        Walk(root, "", (key, _) => map.TryGetValue(key, out var t) ? t : null);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(destPath, root!.ToJsonString(opts), new UTF8Encoding(false));
    }

    // visit(key, value) -> null: değiştirme, aksi halde yeni değer
    private static void Walk(JsonNode? node, string path, Func<string, string, string?> visit)
    {
        if (node is JsonObject obj)
        {
            foreach (var kv in obj.ToList())
            {
                var p = path == "" ? kv.Key : $"{path}.{kv.Key}";
                if (kv.Value is JsonValue v && v.TryGetValue<string>(out var s))
                { var r = visit(p, s); if (r != null) obj[kv.Key] = r; }
                else Walk(kv.Value, p, visit);
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                var p = $"{path}[{i}]";
                if (arr[i] is JsonValue v && v.TryGetValue<string>(out var s))
                { var r = visit(p, s); if (r != null) arr[i] = r; }
                else Walk(arr[i], p, visit);
            }
        }
    }
}
