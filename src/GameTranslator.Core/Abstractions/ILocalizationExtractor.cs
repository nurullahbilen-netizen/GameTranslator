using GameTranslator.Core.Models;

namespace GameTranslator.Core.Abstractions;

public interface ILocalizationExtractor
{
    string Name { get; }
    IReadOnlyCollection<string> SupportedExtensions { get; }
    bool CanHandle(string filePath);
    Task<ExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default);
    Task WriteAsync(string sourceFilePath, IReadOnlyCollection<LocalizationEntry> entries, string outputFilePath, CancellationToken cancellationToken = default);
}
