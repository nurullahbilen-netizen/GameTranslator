# GameTranslator (POC)
```
dotnet new sln -n GameTranslator
dotnet sln add GameTranslator.Core GameTranslator.App
dotnet run --project GameTranslator.App
```
Gereksinim: .NET 8 SDK (Windows). Ollama için: `ollama pull qwen2.5:7b`. DeepL için API anahtarı.
Çıktı, oyun klasöründe `_TR_Output` altına yazılır; orijinal dosyalar değişmez.
