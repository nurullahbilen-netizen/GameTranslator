using System.Text.Json;
using GameTranslator.Core.Models;

namespace GameTranslator.Core.Translation;

public sealed class TranslationCache
{
    private readonly string _path;
    private readonly Dictionary<string, string> _map;

    public TranslationCache(string name)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameTranslator");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, $"cache_{name}.json");
        _map = File.Exists(_path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new() : new();
    }
    public bool TryGet(string k, out string v) => _map.TryGetValue(k, out v!);
    public void Set(string k, string v) => _map[k] = v;
    public void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_map));
}

public sealed class TranslationPipeline
{
    public async Task RunAsync(IReadOnlyList<TextEntry> entries, ITranslationService svc, string src, string tgt,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var cache = new TranslationCache(svc.Name);
        int done = 0, total = entries.Count;
        var todo = new List<TextEntry>();

        foreach (var e in entries)
        {
            if (e.Translation != null) { done++; continue; }
            if (cache.TryGet(e.Source, out var c)) { e.Translation = c; e.Status = EntryStatus.Translated; done++; }
            else todo.Add(e);
        }
        progress?.Report((done, total));

        foreach (var chunk in todo.Chunk(svc.MaxBatchSize))
        {
            ct.ThrowIfCancellationRequested();
            var guarded = chunk.Select(e => PlaceholderGuard.Protect(e.Source)).ToList();
            try
            {
                var res = await svc.TranslateAsync(guarded.Select(g => g.Text).ToList(), src, tgt, ct);
                for (int i = 0; i < chunk.Length; i++)
                {
                    var restored = i < res.Count ? PlaceholderGuard.Restore(res[i], guarded[i].Tokens) : null;
                    if (restored == null) { chunk[i].Status = EntryStatus.Failed; continue; }
                    chunk[i].Translation = restored;
                    chunk[i].Status = EntryStatus.Translated;
                    cache.Set(chunk[i].Source, restored);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { foreach (var e in chunk) e.Status = EntryStatus.Failed; }

            done += chunk.Length;
            progress?.Report((done, total));
            cache.Save();
        }
    }
}
