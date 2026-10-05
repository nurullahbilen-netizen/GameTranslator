using GameTranslator.Core.Inspection;
using GameTranslator.Infrastructure.Games.Unreal;

namespace GameTranslator.Infrastructure.Games.Aion2;

/// <summary>
/// Aion 2 profile entry-point. V1.2 intentionally delegates generic UE detection
/// to UnrealGameInspector so Aion-specific rules can be layered on later.
/// </summary>
public sealed class Aion2Extractor
{
    private readonly UnrealGameInspector _inspector = new();

    public Task<GameInspectionReport> AnalyzeAsync(
        string rootPath,
        IProgress<(int scanned, string current)>? progress = null,
        CancellationToken cancellationToken = default)
        => _inspector.InspectAsync(rootPath, progress, cancellationToken);
}
