namespace GameTranslator.Core.Inspection;

public enum DetectionConfidence
{
    Low,
    Medium,
    High
}

public enum UnrealArchiveMode
{
    None,
    LooseFiles,
    Pak,
    IoStore,
    Mixed
}

public enum ArchiveEncryptionState
{
    Unknown,
    NotDetected,
    LikelyEncrypted
}

public enum InspectionRecommendation
{
    LooseLocres,
    Cue4ParseArchive,
    LooseStructuredFiles,
    OcrFallback
}

public enum CandidateKind
{
    Locres,
    StringTable,
    DataTable,
    StructuredLocalization,
    Font,
    Pak,
    Utoc,
    Ucas,
    Other
}
