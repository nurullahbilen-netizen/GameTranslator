using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Versions;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Inspection;
using GameTranslator.Core.Models;
using GameTranslator.Infrastructure.Extractors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameTranslator.Infrastructure.Games.Unreal.Archive;

/// <summary>
/// Read-only Unreal archive adapter. It never writes into the game directory.
/// It mounts PAK/IoStore containers through CUE4Parse and normalizes discovered
/// localization strings into LocalizationEntry records.
/// </summary>
public sealed class Cue4ParseArchiveProvider : IArchiveLocalizationProvider
{
    private readonly EGame _gameVersion;
    private readonly UnrealLocresExtractor _locresExtractor = new();

    public Cue4ParseArchiveProvider(EGame gameVersion = EGame.GAME_UE5_LATEST)
    {
        _gameVersion = gameVersion;
    }

    public Task<ArchiveEncryptionState> ProbeEncryptionAsync(
        string gameRoot,
        CancellationToken cancellationToken = default)
        => Task.Run(() => ProbeEncryption(gameRoot, cancellationToken), cancellationToken);

    public async Task<ArchiveExtractionResult> ExtractAsync(
        string gameRoot,
        string? aesKey = null,
        IProgress<(int scanned, string current)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(gameRoot))
            throw new DirectoryNotFoundException(gameRoot);

        var result = new ArchiveExtractionResult
        {
            EncryptionState = await ProbeEncryptionAsync(gameRoot, cancellationToken)
        };

        var archiveDir = FindArchiveDirectory(gameRoot);
        if (archiveDir is null)
        {
            result.Warnings.Add(".pak veya .utoc/.ucas arşivi bulunamadı.");
            return result;
        }

        using var provider = new DefaultFileProvider(
            archiveDir,
            SearchOption.TopDirectoryOnly,
            true,
            new VersionContainer(_gameVersion));

        provider.Initialize();

        if (!string.IsNullOrWhiteSpace(aesKey))
        {
            var normalized = NormalizeAesKey(aesKey);
            provider.SubmitKey(new FGuid(), new FAesKey(normalized));
        }

        result.MountedFileCount = provider.Files.Count;

        var files = provider.Files.Values.ToArray();
        for (var i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            progress?.Report((i + 1, file.Path));

            try
            {
                if (file.Extension.Equals("locres", StringComparison.OrdinalIgnoreCase))
                {
                    result.LocresFileCount++;
                    await ExtractLocresAsync(file, result, cancellationToken);
                    continue;
                }

                if (!file.IsUePackage || !file.Extension.Equals("uasset", StringComparison.OrdinalIgnoreCase))
                    continue;

                await ExtractStructuredAssetAsync(provider, file, result, cancellationToken);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"{file.Path}: {ex.Message}");
            }
        }

        return result;
    }

    private ArchiveEncryptionState ProbeEncryption(string gameRoot, CancellationToken token)
    {
        var encrypted = false;
        var sawArchive = false;

        foreach (var pak in Directory.EnumerateFiles(gameRoot, "*.pak", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            sawArchive = true;
            try
            {
                using var reader = new PakFileReader(pak, new VersionContainer(_gameVersion));
                encrypted |= reader.IsEncrypted;
            }
            catch
            {
                // Header parsing can fail for custom game formats; leave state unknown.
                return ArchiveEncryptionState.Unknown;
            }
        }

        foreach (var utoc in Directory.EnumerateFiles(gameRoot, "*.utoc", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            sawArchive = true;
            try
            {
                using var reader = new IoStoreReader(utoc, versions: new VersionContainer(_gameVersion));
                encrypted |= reader.IsEncrypted;
            }
            catch
            {
                return ArchiveEncryptionState.Unknown;
            }
        }

        if (!sawArchive) return ArchiveEncryptionState.Unknown;
        return encrypted ? ArchiveEncryptionState.LikelyEncrypted : ArchiveEncryptionState.NotDetected;
    }

    private async Task ExtractLocresAsync(
        GameFile file,
        ArchiveExtractionResult result,
        CancellationToken cancellationToken)
    {
        var bytes = await file.ReadAsync();
        var temp = Path.Combine(Path.GetTempPath(), $"gt_{Guid.NewGuid():N}.locres");

        try
        {
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
            var extracted = await _locresExtractor.ExtractAsync(temp, cancellationToken);

            foreach (var entry in extracted.Entries)
            {
                result.Entries.Add(new LocalizationEntry
                {
                    Id = entry.Id,
                    SourceText = entry.SourceText,
                    TranslatedText = entry.TranslatedText,
                    SourceFile = $"archive://{file.Path}",
                    Context = entry.Context
                });
            }
        }
        finally
        {
            try { File.Delete(temp); } catch { /* temp cleanup best effort */ }
        }
    }

    private static Task ExtractStructuredAssetAsync(
        DefaultFileProvider provider,
        GameFile file,
        ArchiveExtractionResult result,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var package = provider.LoadPackage(file);
            var exports = package.GetExports().ToArray();
            if (exports.Length == 0) return;

            foreach (var export in exports)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var typeName = export.GetType().Name;
                var isStringTable = typeName.Equals("UStringTable", StringComparison.OrdinalIgnoreCase);
                var isDataTable = typeName.Equals("UDataTable", StringComparison.OrdinalIgnoreCase);

                // Some games use derived/custom classes. Keep a conservative name fallback.
                if (!isStringTable && !isDataTable)
                {
                    isStringTable = typeName.Contains("StringTable", StringComparison.OrdinalIgnoreCase);
                    isDataTable = typeName.Contains("DataTable", StringComparison.OrdinalIgnoreCase);
                }

                if (!isStringTable && !isDataTable) continue;

                if (isStringTable) result.StringTableAssetCount++;
                if (isDataTable) result.DataTableAssetCount++;

                var json = JToken.Parse(JsonConvert.SerializeObject(export));
                var prefix = isStringTable ? "StringTable" : "DataTable";
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var (path, value) in EnumerateStrings(json))
                {
                    if (!LooksLikeTranslatableText(value) || !seen.Add(value))
                        continue;

                    result.Entries.Add(new LocalizationEntry
                    {
                        Id = $"{prefix}:{file.Path}:{path}",
                        SourceText = value,
                        SourceFile = $"archive://{file.Path}",
                        Context = $"{prefix} | {typeName} | {path}"
                    });
                }
            }
        }, cancellationToken);
    }

    private static IEnumerable<(string Path, string Value)> EnumerateStrings(JToken token, string path = "$")
    {
        switch (token)
        {
            case JValue { Type: JTokenType.String, Value: string text }:
                yield return (path, text);
                yield break;

            case JObject obj:
                foreach (var property in obj.Properties())
                    foreach (var item in EnumerateStrings(property.Value, $"{path}.{property.Name}"))
                        yield return item;
                yield break;

            case JArray array:
                for (var i = 0; i < array.Count; i++)
                    foreach (var item in EnumerateStrings(array[i]!, $"{path}[{i}]"))
                        yield return item;
                yield break;
        }
    }

    private static bool LooksLikeTranslatableText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2 || value.Length > 8000)
            return false;

        // Skip obvious object/package paths and technical identifiers.
        if (value.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("/Script/", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            return false;

        return value.Any(char.IsLetter);
    }

    private static string? FindArchiveDirectory(string gameRoot)
    {
        var archive = Directory.EnumerateFiles(gameRoot, "*.utoc", SearchOption.AllDirectories).FirstOrDefault()
                   ?? Directory.EnumerateFiles(gameRoot, "*.pak", SearchOption.AllDirectories).FirstOrDefault();
        return archive is null ? null : Path.GetDirectoryName(archive);
    }

    private static string NormalizeAesKey(string key)
    {
        var value = key.Trim();
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value : "0x" + value;
    }
}
