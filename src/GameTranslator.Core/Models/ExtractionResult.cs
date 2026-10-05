namespace GameTranslator.Core.Models;

public sealed class ExtractionResult
{
    public required string FilePath { get; init; }
    public required string Format { get; init; }
    public List<LocalizationEntry> Entries { get; init; } = new();
}
