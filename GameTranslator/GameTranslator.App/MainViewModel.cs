using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTranslator.Core.Extraction;
using GameTranslator.Core.Models;
using GameTranslator.Core.Translation;
using Microsoft.Win32;

namespace GameTranslator.App;

public partial class MainViewModel : ObservableObject
{
    private readonly ExtractorService _extractor = ExtractorService.CreateDefault();
    private List<SourceFile> _files = new();
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _gameFolder = "";
    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _selectedProvider = "Ollama";
    [ObservableProperty] private string _status = "Hazır";
    [ObservableProperty] private double _progress;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsIdle))] private bool _isBusy;

    public bool IsIdle => !IsBusy;
    public string[] Providers { get; } = { "Ollama", "DeepL" };
    public ObservableCollection<TextEntry> Entries { get; } = new();

    [RelayCommand]
    private void Browse()
    {
        var dlg = new OpenFolderDialog { Title = "Oyun klasörünü seç" };
        if (dlg.ShowDialog() == true) GameFolder = dlg.FolderName;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (!Directory.Exists(GameFolder)) { Status = "Geçerli bir klasör seçin."; return; }
        IsBusy = true; Entries.Clear();
        try
        {
            _files = await _extractor.ScanAsync(GameFolder, new Progress<string>(s => Status = s));
            foreach (var e in _files.SelectMany(f => f.Entries)) Entries.Add(e);
            Status = $"{_files.Count} dosya, {Entries.Count} metin bulundu.";
        }
        catch (Exception ex) { Status = "Hata: " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TranslateAsync()
    {
        if (Entries.Count == 0) return;
        ITranslationService svc = SelectedProvider == "DeepL" ? new DeepLTranslationService(ApiKey) : new OllamaTranslationService();
        _cts = new CancellationTokenSource();
        var prog = new Progress<(int Done, int Total)>(p =>
        { Progress = 100.0 * p.Done / Math.Max(1, p.Total); Status = $"Çevriliyor: {p.Done}/{p.Total}"; });
        IsBusy = true;
        try
        {
            await new TranslationPipeline().RunAsync(Entries.ToList(), svc, "EN", "TR", prog, _cts.Token);
            Status = $"Bitti. Başarısız: {Entries.Count(e => e.Status == EntryStatus.Failed)}";
        }
        catch (OperationCanceledException) { Status = "İptal edildi."; }
        catch (Exception ex) { Status = "Hata: " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void Export()
    {
        var outDir = Path.Combine(GameFolder, "_TR_Output");
        _extractor.Export(_files, GameFolder, outDir);
        Status = $"Yazıldı: {outDir} (orijinal dosyalara dokunulmadı)";
    }

    [RelayCommand]
    private void ExportDictionary()
    {
        var path = Path.Combine(GameFolder, "tr_dictionary.tsv");
        ExtractorService.ExportDictionary(Entries, path);
        Status = $"Sözlük yazıldı: {path}";
    }
}
