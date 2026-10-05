using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.StringTable;
using CUE4Parse.UE4.Assets.Exports;

namespace GameTranslator.Infrastructure.Games.Unreal.Archive;

public static class StringTableReader
{
    public static IEnumerable<GameFile> FindPackageCandidates(DefaultFileProvider provider)
    {
        return provider.Files.Values.Where(f =>
            f.IsUePackage && (
                f.Path.Contains("StringTable", StringComparison.OrdinalIgnoreCase) ||
                f.Path.Contains("DataTable", StringComparison.OrdinalIgnoreCase) ||
                f.Path.Contains("Localization", StringComparison.OrdinalIgnoreCase)
            ));
    }
}
