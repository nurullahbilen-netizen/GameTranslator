using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Inspection;
using GameTranslator.Core.Models;
using GameTranslator.Core.Text;
using GameTranslator.Core.Translation;
using GameTranslator.Infrastructure.Extractors;
using GameTranslator.Infrastructure.Games.Aion2;
using GameTranslator.Infrastructure.Games.Unreal.Archive;
using GameTranslator.Infrastructure.Services;
using GameTranslator.Infrastructure.Translation;
using Microsoft.Win32;

namespace GameTranslator.App;

public partial class MainWindow : Window
{
    private readonly List<ILocalizationExtractor> _extractors =
    [
        new JsonLocalizationExtractor(),
        new XmlLocalizationExtractor(),
        new CsvLocalizationExtractor(),
        new PoLocalizationExtractor(),
        new UnrealLocresExtractor()
    ];

    private readonly ITranslationService _translator = new PlaceholderSafeTranslationService(
        new MockTranslationService(),
        new PlaceholderProtector());

    private readonly Aion2Extractor _aionInspector = new();
    private readonly IArchiveLocalizationProvider _archiveProvider = new Cue4ParseArchiveProvider();
    private readonly ObservableCollection<LocalizationEntry> _entries = new();
    private readonly Dictionary<string, ExtractionResult> _results = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _files = new();
    private CancellationTokenSource? _inspectionCts;

    public MainWindow()
    {
        InitializeComponent();
        EntriesGrid.ItemsSource = _entries;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Aion 2 / Unreal Engine oyun klasörünü seç" };
        if (dialog.ShowDialog() == true)
            GamePathBox.Text = dialog.FolderName;
    }

    private async void Inspect_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(GamePathBox.Text))
        {
            MessageBox.Show("Geçerli bir oyun klasörü seç.");
            return;
        }

        _inspectionCts?.Cancel();
        _inspectionCts = new CancellationTokenSource();
        SetBusy(true, "Oyun klasörü analiz ediliyor...");

        var progress = new Progress<(int scanned, string current)>(x =>
        {
            StatusText.Text = $"Analiz: {x.scanned:N0} dosya tarandı - {Path.GetFileName(x.current)}";
            Progress.IsIndeterminate = true;
        });

        try
        {
            var report = await _aionInspector.AnalyzeAsync(
                GamePathBox.Text,
                progress,
                _inspectionCts.Token);

            if (report.PakCount + report.UtocCount > 0)
            {
                report.EncryptionState = await _archiveProvider.ProbeEncryptionAsync(
                    GamePathBox.Text,
                    _inspectionCts.Token);
                report.Evidence.Add(new InspectionEvidence(
                    "Encryption",
                    $"CUE4Parse header probe: {report.EncryptionState}"));
            }

            ShowInspectionReport(report);
            StatusText.Text = $"Analiz tamamlandı. {report.Candidates.Count:N0} aday bulundu.";
            MainTabs.SelectedIndex = 0;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Analiz iptal edildi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Analiz sırasında hata oluştu:\n{ex.Message}", "Game Inspector");
            StatusText.Text = "Analiz başarısız.";
        }
        finally
        {
            Progress.IsIndeterminate = false;
            SetBusy(false);
        }
    }

    private void ShowInspectionReport(GameInspectionReport report)
    {
        EngineText.Text = report.EngineVersion == "Unknown"
            ? report.EngineName
            : $"{report.EngineName} {report.EngineVersion}";
        EngineConfidenceText.Text = $"Güven: {ToTr(report.EngineConfidence)}";

        ArchiveModeText.Text = ToTr(report.ArchiveMode);
        EncryptionText.Text = $"Şifreleme: {ToTr(report.EncryptionState)} | PAK {report.PakCount}, UTOC {report.UtocCount}, UCAS {report.UcasCount}";

        LocresCountText.Text = report.LocresCount.ToString("N0");
        StructuredCountText.Text = $"JSON/XML/CSV/PO/LANG: {report.StructuredLocalizationCount:N0}";
        TableCountText.Text = $"{report.StringTableCount:N0} / {report.DataTableCount:N0}";
        FontCountText.Text = $"Font adayı: {report.FontCount:N0}";

        RecommendationText.Text = ToTr(report.Recommendation);
        RecommendationReasonText.Text = report.RecommendationReason;

        InspectionCandidatesGrid.ItemsSource = report.Candidates;
        EvidenceGrid.ItemsSource = report.Evidence;

        var notes = new List<string>();
        notes.AddRange(report.Warnings.Select(x => "• " + x));
        if (report.EncryptionState == ArchiveEncryptionState.Unknown && (report.PakCount + report.UtocCount) > 0)
            notes.Add("• Arşiv şifrelemesi yalnızca dosya adlarından güvenilir biçimde belirlenemez. CUE4Parse mount denemesi bu alanı doğrulayacak.");
        if (report.StringTableCount + report.DataTableCount == 0 && (report.PakCount + report.UtocCount) > 0)
            notes.Add("• StringTable/DataTable adayları arşiv içinde olabilir; V1.3 Inspector loose adayları sayar; CUE4Parse arşiv çıkarımı Metinler sekmesinden çalıştırılabilir.");
        WarningsText.Text = notes.Count == 0 ? "Belirgin bir uyarı yok." : string.Join(Environment.NewLine, notes);
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(GamePathBox.Text))
        {
            MessageBox.Show("Geçerli bir oyun klasörü seç.");
            return;
        }

        SetBusy(true, "Yerelleştirme dosyaları taranıyor...");
        try
        {
            var scanner = new GameScanner(_extractors);
            _files = (await scanner.ScanAsync(GamePathBox.Text)).ToList();
            StatusText.Text = $"{_files.Count} desteklenen yerelleştirme dosyası bulundu.";
        }
        finally { SetBusy(false); }
    }

    private async void Extract_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0)
        {
            MessageBox.Show("Önce Metinler sekmesinde Tara butonuna bas.");
            return;
        }

        _entries.Clear();
        _results.Clear();
        SetBusy(true, "Metinler çıkarılıyor...");

        try
        {
            for (var i = 0; i < _files.Count; i++)
            {
                var file = _files[i];
                var extractor = _extractors.FirstOrDefault(x => x.CanHandle(file));
                if (extractor is null) continue;

                try
                {
                    var result = await extractor.ExtractAsync(file);
                    _results[file] = result;
                    foreach (var entry in result.Entries)
                        _entries.Add(entry);
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Atlandı: {Path.GetFileName(file)} - {ex.Message}";
                }

                Progress.Value = (i + 1) * 100d / _files.Count;
            }

            StatusText.Text = $"{_entries.Count} metin çıkarıldı.";
        }
        finally { SetBusy(false); }
    }


    private async void ExtractArchive_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(GamePathBox.Text))
        {
            MessageBox.Show("Geçerli bir oyun klasörü seç.");
            return;
        }

        SetBusy(true, "CUE4Parse ile Unreal arşivleri okunuyor...");
        _entries.Clear();

        var progress = new Progress<(int scanned, string current)>(x =>
        {
            StatusText.Text = $"Arşiv: {x.scanned:N0} öğe - {x.current}";
            Progress.IsIndeterminate = true;
        });

        try
        {
            var result = await _archiveProvider.ExtractAsync(
                GamePathBox.Text,
                string.IsNullOrWhiteSpace(AesKeyBox.Text) ? null : AesKeyBox.Text,
                progress);

            foreach (var entry in result.Entries)
                _entries.Add(entry);

            MainTabs.SelectedIndex = 1;
            StatusText.Text = $"Arşiv taraması tamamlandı: {_entries.Count:N0} metin | LocRes {result.LocresFileCount} | StringTable {result.StringTableAssetCount} | DataTable {result.DataTableAssetCount}.";

            if (result.Warnings.Count > 0)
                MessageBox.Show(string.Join(Environment.NewLine, result.Warnings.Take(12)), "CUE4Parse uyarıları");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Arşiv okuma hatası:\n{ex.Message}", "CUE4Parse");
            StatusText.Text = "Arşiv taraması başarısız.";
        }
        finally
        {
            Progress.IsIndeterminate = false;
            SetBusy(false);
        }
    }

    private async void Translate_Click(object sender, RoutedEventArgs e)
    {
        if (_entries.Count == 0) return;
        SetBusy(true, $"Çeviri: {_translator.Name}");

        try
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                entry.TranslatedText = await _translator.TranslateAsync(entry.SourceText, "English", "Turkish");
                EntriesGrid.Items.Refresh();
                Progress.Value = (i + 1) * 100d / _entries.Count;
            }
            StatusText.Text = "Çeviri tamamlandı.";
        }
        finally { SetBusy(false); }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_results.Count == 0) return;
        var exportRoot = Path.Combine(GamePathBox.Text, "GameTranslator_Output");
        Directory.CreateDirectory(exportRoot);
        SetBusy(true, "Dosyalar dışa aktarılıyor...");

        try
        {
            var index = 0;
            foreach (var pair in _results)
            {
                var extractor = _extractors.First(x => x.CanHandle(pair.Key));
                var relative = Path.GetRelativePath(GamePathBox.Text, pair.Key);
                var outPath = Path.Combine(exportRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                await extractor.WriteAsync(pair.Key, pair.Value.Entries, outPath);
                Progress.Value = (++index) * 100d / _results.Count;
            }

            StatusText.Text = $"Çıktılar: {exportRoot}";
            MessageBox.Show("Orijinal oyun dosyalarına dokunulmadı. Çevrilmiş kopyalar GameTranslator_Output klasörüne yazıldı.");
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy, string? text = null)
    {
        if (text is not null) StatusText.Text = text;
        if (busy) Progress.Value = 0;
    }

    private static string ToTr(DetectionConfidence value) => value switch
    {
        DetectionConfidence.High => "Yüksek",
        DetectionConfidence.Medium => "Orta",
        _ => "Düşük"
    };

    private static string ToTr(UnrealArchiveMode value) => value switch
    {
        UnrealArchiveMode.LooseFiles => "Loose Files",
        UnrealArchiveMode.Pak => "Klasik PAK",
        UnrealArchiveMode.IoStore => "IoStore (.utoc/.ucas)",
        UnrealArchiveMode.Mixed => "Karma",
        _ => "Bulunamadı"
    };

    private static string ToTr(ArchiveEncryptionState value) => value switch
    {
        ArchiveEncryptionState.NotDetected => "Tespit edilmedi",
        ArchiveEncryptionState.LikelyEncrypted => "Muhtemelen şifreli",
        _ => "Bilinmiyor"
    };

    private static string ToTr(InspectionRecommendation value) => value switch
    {
        InspectionRecommendation.LooseLocres => "Loose Locres",
        InspectionRecommendation.Cue4ParseArchive => "CUE4Parse Archive",
        InspectionRecommendation.LooseStructuredFiles => "Loose Structured Files",
        _ => "OCR Fallback"
    };
}
