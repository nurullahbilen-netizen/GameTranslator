using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;

namespace GameTranslator.Infrastructure.Games.Unreal.Archive;

public sealed class Cue4ParseArchiveProvider
{
    public DefaultFileProvider CreateProvider(UnrealArchiveOptions options)
    {
        var provider = new DefaultFileProvider(
            options.GameRoot,
            SearchOption.AllDirectories,
            true,
            new VersionContainer(options.GameVersion));

        if (!string.IsNullOrWhiteSpace(options.UsmapPath))
        {
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(options.UsmapPath);
        }

        provider.Initialize();

        if (!string.IsNullOrWhiteSpace(options.AesKey))
        {
            provider.SubmitKey(new FGuid(), new FAesKey(options.AesKey));
        }

        return provider;
    }
}
