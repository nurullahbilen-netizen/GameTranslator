namespace GameTranslator.Core.Translation;

public interface ITranslationService
{
    string Name { get; }
    int MaxBatchSize { get; }
    Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string sourceLang, string targetLang, CancellationToken ct = default);
}
