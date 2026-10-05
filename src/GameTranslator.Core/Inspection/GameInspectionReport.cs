namespace GameTranslator.Core.Inspection;

public sealed class GameInspectionReport
{
    public required string RootPath { get; init; }
    public string EngineName { get; set; } = "Unknown";
    public string EngineVersion { get; set; } = "Unknown";
    public DetectionConfidence EngineConfidence { get; set; } = DetectionConfidence.Low;
    public UnrealArchiveMode ArchiveMode { get; set; } = UnrealArchiveMode.None;
    public ArchiveEncryptionState EncryptionState { get; set; } = ArchiveEncryptionState.Unknown;
    public InspectionRecommendation Recommendation { get; set; } = InspectionRecommendation.OcrFallback;
    public string RecommendationReason { get; set; } = string.Empty;
    public DateTimeOffset ScannedAt { get; init; } = DateTimeOffset.Now;

    public List<GameAssetCandidate> Candidates { get; } = new();
    public List<InspectionEvidence> Evidence { get; } = new();
    public List<string> Warnings { get; } = new();

    public int LocresCount => Candidates.Count(x => x.Kind == CandidateKind.Locres);
    public int StringTableCount => Candidates.Count(x => x.Kind == CandidateKind.StringTable);
    public int DataTableCount => Candidates.Count(x => x.Kind == CandidateKind.DataTable);
    public int StructuredLocalizationCount => Candidates.Count(x => x.Kind == CandidateKind.StructuredLocalization);
    public int FontCount => Candidates.Count(x => x.Kind == CandidateKind.Font);
    public int PakCount => Candidates.Count(x => x.Kind == CandidateKind.Pak);
    public int UtocCount => Candidates.Count(x => x.Kind == CandidateKind.Utoc);
    public int UcasCount => Candidates.Count(x => x.Kind == CandidateKind.Ucas);

    public IEnumerable<GameAssetCandidate> LocalizationCandidates => Candidates.Where(x =>
        x.Kind is CandidateKind.Locres or CandidateKind.StringTable or CandidateKind.DataTable or CandidateKind.StructuredLocalization);
}
