using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace GameTranslator.Core.Translation;

public sealed class DeepLTranslationService(string apiKey, HttpClient? http = null) : ITranslationService
{
    private readonly HttpClient _http = http ?? new HttpClient();
    public string Name => "DeepL";
    public int MaxBatchSize => 40;

    public async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var host = apiKey.EndsWith(":fx") ? "api-free.deepl.com" : "api.deepl.com";
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{host}/v2/translate")
        { Content = JsonContent.Create(new { text = texts, target_lang = targetLang.ToUpperInvariant() }) };
        req.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", apiKey);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
        return json!["translations"]!.AsArray().Select(t => t!["text"]!.GetValue<string>()).ToList();
    }
}
