namespace GameTranslator.Core.Inspection;

public sealed record InspectionEvidence(
    string Category,
    string Message,
    string? Path = null);
