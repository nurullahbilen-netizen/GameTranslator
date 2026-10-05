namespace GameTranslator.Core.Inspection;

public sealed record GameAssetCandidate(
    CandidateKind Kind,
    string Path,
    string Reason)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}
