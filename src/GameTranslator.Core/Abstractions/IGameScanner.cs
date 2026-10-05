namespace GameTranslator.Core.Abstractions;

public interface IGameScanner
{
    Task<IReadOnlyList<string>> ScanAsync(string rootPath, CancellationToken cancellationToken = default);
}
