using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GameTranslator.Core.Abstractions;

namespace GameTranslator.Infrastructure.Translation;

public sealed class OllamaTranslationService(HttpClient httpClient, string model = "qwen2.5:7b") : ITranslationService
{
    public string Name => "Ollama";

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default)
    {
        var prompt = $"Translate the following game text from {sourceLanguage} to {targetLanguage}. Preserve placeholders like {{0}}, %s, <tag>, and escape sequences. Return only the translation.\n\n{text}";
        var response = await httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", new
        {
            model,
            prompt,
            stream = false
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken);
        return payload?.Response?.Trim() ?? string.Empty;
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
