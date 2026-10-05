namespace GameTranslator.Core.Models;

public sealed class LocalizationEntry
{
    public required string Id { get; init; }
    public required string SourceText { get; set; }
    public string? TranslatedText { get; set; }
    public required string SourceFile { get; init; }
    public string? Context { get; init; }
}
