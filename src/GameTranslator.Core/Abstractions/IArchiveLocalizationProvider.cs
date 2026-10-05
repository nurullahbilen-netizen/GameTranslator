using GameTranslator.Core.Inspection;

namespace GameTranslator.Core.Abstractions;

public interface IArchiveLocalizationProvider
{
    Task<ArchiveEncryptionState> ProbeEncryptionAsync(
        string gameRoot,
        CancellationToken cancellationToken = default);

    Task<ArchiveExtractionResult> ExtractAsync(
        string gameRoot,
        string? aesKey = null,
        IProgress<(int scanned, string current)>? progress = null,
        CancellationToken cancellationToken = default);
}
