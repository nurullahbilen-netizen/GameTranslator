namespace GameTranslator.Infrastructure.Games.Aion2;

[Obsolete("V1.2 uses GameTranslator.Core.Inspection.GameInspectionReport.")]
public sealed class Aion2AnalysisResult
{
    public required string RootPath { get; init; }
    public List<string> LocalizationFiles { get; init; } = new();
    public List<string> UnrealArchives { get; init; } = new();
    public List<string> FontCandidates { get; init; } = new();
}
