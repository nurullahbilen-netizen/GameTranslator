using System.Diagnostics;
using System.Text.Json;
using GameTranslator.Core.Inspection;

namespace GameTranslator.Infrastructure.Games.Unreal;

/// <summary>
/// Read-only inspector for packaged Unreal Engine games.
/// It never modifies archives, bypasses encryption, or injects into a process.
/// Exact archive parsing is intentionally delegated to a later CUE4Parse provider.
/// </summary>
public sealed class UnrealGameInspector
{
    private static readonly HashSet<string> StructuredLocalizationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".xml", ".csv", ".po", ".lang"
    };

    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf", ".otf", ".ufont"
    };

    public async Task<GameInspectionReport> InspectAsync(
        string rootPath,
        IProgress<(int scanned, string current)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException(rootPath);

        return await Task.Run(() => InspectCore(rootPath, progress, cancellationToken), cancellationToken);
    }

    private static GameInspectionReport InspectCore(
        string rootPath,
        IProgress<(int scanned, string current)>? progress,
        CancellationToken cancellationToken)
    {
        var report = new GameInspectionReport { RootPath = rootPath };
        var dirs = new Stack<string>();
        dirs.Push(rootPath);
        var scanned = 0;
        var executableCandidates = new List<string>();

        while (dirs.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dir = dirs.Pop();

            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                    dirs.Push(sub);

                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    scanned++;
                    if ((scanned & 127) == 0)
                        progress?.Report((scanned, file));

                    ClassifyFile(file, report, executableCandidates);
                }
            }
            catch (UnauthorizedAccessException)
            {
                report.Warnings.Add($"Erişim reddedildi: {dir}");
            }
            catch (IOException ex)
            {
                report.Warnings.Add($"Okuma hatası: {dir} - {ex.Message}");
            }
        }

        DetectEngine(rootPath, report, executableCandidates);
        DetermineArchiveMode(report);
        DetermineRecommendation(report);

        report.Candidates.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path));
        progress?.Report((scanned, "Tamamlandı"));
        return report;
    }

    private static void ClassifyFile(
        string file,
        GameInspectionReport report,
        List<string> executableCandidates)
    {
        var ext = Path.GetExtension(file);
        var normalized = file.Replace('\\', '/');
        var name = Path.GetFileNameWithoutExtension(file);

        switch (ext.ToLowerInvariant())
        {
            case ".locres":
                report.Candidates.Add(new(CandidateKind.Locres, file, "Unreal compiled localization resource"));
                return;
            case ".pak":
                report.Candidates.Add(new(CandidateKind.Pak, file, "Classic Unreal PAK archive"));
                return;
            case ".utoc":
                report.Candidates.Add(new(CandidateKind.Utoc, file, "Unreal IoStore table of contents"));
                return;
            case ".ucas":
                report.Candidates.Add(new(CandidateKind.Ucas, file, "Unreal IoStore data container"));
                return;
            case ".exe":
                executableCandidates.Add(file);
                break;
        }

        if (FontExtensions.Contains(ext))
        {
            report.Candidates.Add(new(CandidateKind.Font, file, "Font asset"));
            return;
        }

        if (StructuredLocalizationExtensions.Contains(ext) && LooksLikeLocalizationPath(normalized))
        {
            report.Candidates.Add(new(CandidateKind.StructuredLocalization, file, "Localization-like structured text file"));
            return;
        }

        // Loose .uasset candidates can be inspected without opening package internals.
        // This is intentionally heuristic: actual object class verification belongs to CUE4Parse.
        if (ext.Equals(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("StringTable", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("/StringTables/", StringComparison.OrdinalIgnoreCase))
            {
                report.Candidates.Add(new(CandidateKind.StringTable, file, "Loose .uasset path/name suggests UStringTable"));
            }
            else if (name.Contains("DataTable", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Contains("/DataTables/", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Contains("/Table/", StringComparison.OrdinalIgnoreCase))
            {
                report.Candidates.Add(new(CandidateKind.DataTable, file, "Loose .uasset path/name suggests UDataTable"));
            }
        }
    }

    private static void DetectEngine(
        string rootPath,
        GameInspectionReport report,
        List<string> executableCandidates)
    {
        var buildVersion = FindBuildVersion(rootPath);
        if (buildVersion is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(buildVersion));
                var root = doc.RootElement;
                var major = GetInt(root, "MajorVersion");
                var minor = GetInt(root, "MinorVersion");
                var patch = GetInt(root, "PatchVersion");
                var branch = GetString(root, "BranchName");

                if (major is > 0)
                {
                    report.EngineName = "Unreal Engine";
                    report.EngineVersion = $"{major}.{minor ?? 0}.{patch ?? 0}";
                    report.EngineConfidence = DetectionConfidence.High;
                    report.Evidence.Add(new("Engine", $"Engine/Build/Build.version bulundu ({report.EngineVersion})", buildVersion));
                    if (!string.IsNullOrWhiteSpace(branch))
                        report.Evidence.Add(new("Engine", $"Branch: {branch}", buildVersion));
                    return;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                report.Warnings.Add($"Build.version okunamadı: {ex.Message}");
            }
        }

        // Packaged games often omit Build.version. File metadata can still provide a clue,
        // but we deliberately avoid presenting a guessed engine minor version as fact.
        foreach (var exe in executableCandidates.OrderByDescending(File.GetLastWriteTimeUtc).Take(12))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exe);
                var text = string.Join(" ", info.ProductName, info.FileDescription, info.Comments, info.FileVersion);
                if (text.Contains("Unreal", StringComparison.OrdinalIgnoreCase))
                {
                    report.EngineName = "Unreal Engine";
                    report.EngineVersion = "Unknown (packaged build metadata)";
                    report.EngineConfidence = DetectionConfidence.Medium;
                    report.Evidence.Add(new("Engine", "EXE sürüm metadatasında Unreal işareti bulundu", exe));
                    return;
                }
            }
            catch { /* metadata is optional */ }
        }

        var hasUnrealLayout = Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Any(x => Path.GetFileName(x).EndsWith("Game", StringComparison.OrdinalIgnoreCase))
            || report.PakCount > 0 || report.UtocCount > 0 || report.LocresCount > 0;

        if (hasUnrealLayout)
        {
            report.EngineName = "Unreal Engine";
            report.EngineVersion = report.UtocCount > 0
                ? "Unknown (IoStore present; exact version requires package parsing)"
                : "Unknown";
            report.EngineConfidence = DetectionConfidence.Medium;
            report.Evidence.Add(new("Engine", "Unreal archive/localization/layout indicators detected"));
        }
    }

    private static string? FindBuildVersion(string rootPath)
    {
        var direct = Path.Combine(rootPath, "Engine", "Build", "Build.version");
        if (File.Exists(direct)) return direct;

        try
        {
            return Directory.EnumerateFiles(rootPath, "Build.version", SearchOption.AllDirectories)
                .FirstOrDefault(x => x.Replace('\\', '/').Contains("/Engine/Build/", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static int? GetInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static void DetermineArchiveMode(GameInspectionReport report)
    {
        var hasPak = report.PakCount > 0;
        var hasIo = report.UtocCount > 0 || report.UcasCount > 0;
        var hasLoose = report.LocresCount > 0 || report.StructuredLocalizationCount > 0 || report.StringTableCount > 0 || report.DataTableCount > 0;

        report.ArchiveMode = (hasPak, hasIo, hasLoose) switch
        {
            (false, false, true) => UnrealArchiveMode.LooseFiles,
            (true, false, false) => UnrealArchiveMode.Pak,
            (false, true, false) => UnrealArchiveMode.IoStore,
            (false, false, false) => UnrealArchiveMode.None,
            _ => UnrealArchiveMode.Mixed
        };

        // File-name scanning cannot reliably prove AES encryption.
        // A later archive provider should update this when mounting an index.
        report.EncryptionState = (hasPak || hasIo)
            ? ArchiveEncryptionState.Unknown
            : ArchiveEncryptionState.NotDetected;
    }

    private static void DetermineRecommendation(GameInspectionReport report)
    {
        if (report.LocresCount > 0)
        {
            report.Recommendation = InspectionRecommendation.LooseLocres;
            report.RecommendationReason = "Loose .locres bulundu; en düşük riskli ve en doğrudan yol önce bunları kullanmak.";
            return;
        }

        if (report.StructuredLocalizationCount > 0)
        {
            report.Recommendation = InspectionRecommendation.LooseStructuredFiles;
            report.RecommendationReason = "JSON/XML/CSV/PO/LANG yerelleştirme adayları bulundu; arşive girmeden önce bunları işlemek daha güvenli.";
            return;
        }

        if (report.PakCount > 0 || report.UtocCount > 0 || report.UcasCount > 0)
        {
            report.Recommendation = InspectionRecommendation.Cue4ParseArchive;
            report.RecommendationReason = "Metin adayları loose dosyalarda bulunamadı fakat Unreal arşivleri mevcut; sonraki adım salt-okunur CUE4Parse analizidir.";
            return;
        }

        report.Recommendation = InspectionRecommendation.OcrFallback;
        report.RecommendationReason = "Desteklenen yerelleştirme veya Unreal arşiv göstergesi bulunamadı; OCR yalnızca son seçenek olarak öneriliyor.";
    }

    private static bool LooksLikeLocalizationPath(string normalized) =>
        normalized.Contains("Localization", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("/L10N/", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("/Locale/", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("/Locales/", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("/Language/", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("/Languages/", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("String", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("Text", StringComparison.OrdinalIgnoreCase);
}
