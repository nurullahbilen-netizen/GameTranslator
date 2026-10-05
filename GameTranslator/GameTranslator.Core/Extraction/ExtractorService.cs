using System.Text;
using GameTranslator.Core.Models;

namespace GameTranslator.Core.Extraction;

public sealed record SourceFile(string Path, ILocalizationHandler Handler, IReadOnlyList<TextEntry> Entries);

public sealed class ExtractorService
{
    private static readonly string[] Hints = { "local", "lang", "string", "i18n", "text", "dialog", "translation" };
    private readonly IReadOnlyList<ILocalizationHandler> _handlers;

    public ExtractorService(IEnumerable<ILocalizationHandler> handlers) => _handlers = handlers.ToList();

    public static ExtractorService CreateDefault() => new(new ILocalizationHandler[]
        { new JsonLocalizationHandler(), new PoLocalizationHandler(), new XmlLocalizationHandler() });

    public Task<List<SourceFile>> ScanAsync(string root, IProgress<string>? progress = null, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var result = new List<SourceFile>();
            foreach (var path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var handler = _handlers.FirstOrDefault(h => h.CanHandle(path));
                if (handler == null) continue;
                var rel = Path.GetRelativePath(root, path);
                if (handler.Name != "PO" && !Hints.Any(k => rel.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
                try
                {
                    progress?.Report($"Taranıyor: {rel}");
                    var entries = handler.Extract(path);
                    if (entries.Count > 0) result.Add(new SourceFile(path, handler, entries));
                }
                catch (Exception ex) { progress?.Report($"Atlandı ({rel}): {ex.Message}"); }
            }
            return result;
        }, ct);

    /// <summary>Çevirileri geri yazar. inPlace=false: outDir altına aynı klasör yapısıyla; true: .bak yedeği alıp üzerine yazar.</summary>
    public void Export(IEnumerable<SourceFile> files, string root, string outDir, bool inPlace = false)
    {
        foreach (var f in files)
        {
            if (!f.Entries.Any(e => e.Translation != null)) continue;
            string src = f.Path, dst;
            if (inPlace)
            {
                var bak = f.Path + ".bak";
                if (!File.Exists(bak)) File.Copy(f.Path, bak);
                src = bak; dst = f.Path;
            }
            else dst = Path.Combine(outDir, Path.GetRelativePath(root, f.Path));
            f.Handler.Write(src, dst, f.Entries);
        }
    }

    /// <summary>Native hook DLL'in okuyacağı tek satırlık "orijinal\tçeviri" sözlüğü.</summary>
    public static void ExportDictionary(IEnumerable<TextEntry> entries, string path) =>
        File.WriteAllLines(path, entries
            .Where(e => e.Translation != null && !(e.Source + e.Translation).Contains('\n') && !(e.Source + e.Translation).Contains('\t'))
            .GroupBy(e => e.Source).Select(g => g.Key + "\t" + g.First().Translation), new UTF8Encoding(false));
}
