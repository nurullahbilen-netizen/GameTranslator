using GameTranslator.Core.Models;

namespace GameTranslator.Core.Inspection;

public sealed class ArchiveExtractionResult
{
    public ArchiveEncryptionState EncryptionState { get; init; } = ArchiveEncryptionState.Unknown;
    public List<LocalizationEntry> Entries { get; } = new();
    public List<string> Warnings { get; } = new();
    public int MountedFileCount { get; set; }
    public int LocresFileCount { get; set; }
    public int StringTableAssetCount { get; set; }
    public int DataTableAssetCount { get; set; }
}
