using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Localization;

namespace GameTranslator.Infrastructure.Games.Unreal.Archive;

public static class LocresReader
{
    public static IEnumerable<GameFile> FindLocresFiles(DefaultFileProvider provider)
    {
        return provider.Files.Values
            .Where(f => f.Path.EndsWith(".locres", StringComparison.OrdinalIgnoreCase));
    }

    public static FTextLocalizationResource ReadLocres(GameFile file)
    {
        using var archive = file.CreateReader();
        return new FTextLocalizationResource(archive);
    }
}
