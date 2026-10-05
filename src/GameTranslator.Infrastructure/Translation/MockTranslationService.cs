using GameTranslator.Core.Abstractions;

namespace GameTranslator.Infrastructure.Translation;

public sealed class MockTranslationService : ITranslationService
{
    public string Name => "Mock / Offline POC";
    public Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default)
        => Task.FromResult($"[TR] {text}");
}
