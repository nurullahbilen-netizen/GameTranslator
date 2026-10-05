namespace GameTranslator.Core.Abstractions;

public interface ITranslationService
{
    string Name { get; }
    Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default);
}
