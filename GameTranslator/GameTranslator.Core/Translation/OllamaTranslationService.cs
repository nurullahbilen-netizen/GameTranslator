using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace GameTranslator.Core.Translation;

/// <summary>Yerel LLM (Ollama /api/chat). Model değiştirmek için ctor parametresi yeterli.</summary>
public sealed class OllamaTranslationService(string model = "qwen2.5:7b", string baseUrl = "http://localhost:11434", HttpClient? http = null) : ITranslationService
{
    private readonly HttpClient _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    public string Name => "Ollama-" + model.Replace(':', '_');
    public int MaxBatchSize => 10;

    public async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var output = new List<string>();
        foreach (var text in texts)
        {
            var body = new
            {
                model, stream = false,
                messages = new object[]
                {
                    new { role = "system", content = $"You are a professional video game localizer. Translate from {sourceLang} to {targetLang} (natural Turkish for gamers). Keep tokens like §0§ exactly as they are. Output only the translation." },
                    new { role = "user", content = text }
                }
            };
            using var res = await _http.PostAsJsonAsync($"{baseUrl}/api/chat", body, ct);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
            output.Add(json!["message"]!["content"]!.GetValue<string>().Trim());
        }
        return output;
    }
}
