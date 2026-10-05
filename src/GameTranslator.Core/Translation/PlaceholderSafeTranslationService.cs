using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Text;

namespace GameTranslator.Core.Translation;

/// <summary>
/// Decorator that makes any ITranslationService placeholder-safe.
/// </summary>
public sealed class PlaceholderSafeTranslationService(
    ITranslationService inner,
    PlaceholderProtector protector) : ITranslationService
{
    public string Name => $"{inner.Name} + Placeholder Protection";

    public async Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        var protectedText = protector.Protect(text);
        var translated = await inner.TranslateAsync(
            protectedText.Text,
            sourceLanguage,
            targetLanguage,
            cancellationToken);

        return protector.RestoreAndValidate(protectedText, translated);
    }
}
