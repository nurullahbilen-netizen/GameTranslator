using GameTranslator.Core.Abstractions;

namespace GameTranslator.Infrastructure.Services;

public sealed class GameScanner(IEnumerable<ILocalizationExtractor> extractors) : IGameScanner
{
    private readonly HashSet<string> _extensions = extractors
        .SelectMany(x => x.SupportedExtensions)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<string>> ScanAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<string>>(() =>
        {
            var result = new List<string>();
            var pending = new Stack<string>();
            pending.Push(rootPath);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dir = pending.Pop();
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir)) pending.Push(sub);
                    foreach (var file in Directory.EnumerateFiles(dir))
                        if (_extensions.Contains(Path.GetExtension(file))) result.Add(file);
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            return result;
        }, cancellationToken);
    }
}
