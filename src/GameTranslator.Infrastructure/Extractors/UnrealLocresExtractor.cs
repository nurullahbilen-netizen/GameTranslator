using System.Collections;
using System.Reflection;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Models;

namespace GameTranslator.Infrastructure.Extractors;

/// <summary>
/// Adapter for Unreal Engine .locres files.
/// To keep the main solution dependency-light, LocresLib is loaded dynamically.
/// Place LocresLib.dll beside the app executable or in a ./lib directory.
/// </summary>
public sealed class UnrealLocresExtractor : ILocalizationExtractor
{
    public string Name => "Unreal Engine LocRes";
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".locres"];

    public bool CanHandle(string filePath)
        => string.Equals(Path.GetExtension(filePath), ".locres", StringComparison.OrdinalIgnoreCase);

    public async Task<ExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var api = LoadLocresApi();

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileObject = api.CreateFile();

            using (var stream = File.OpenRead(filePath))
                api.Load(fileObject, stream);

            var result = new ExtractionResult
            {
                FilePath = filePath,
                Format = "Unreal .locres"
            };

            foreach (var nsItem in api.Enumerate(fileObject))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nsName = api.GetPairKey(nsItem) ?? string.Empty;
                var nsValue = api.GetPairValue(nsItem) ?? nsItem;

                foreach (var entryItem in api.Enumerate(nsValue))
                {
                    var key = api.GetPairKey(entryItem) ?? string.Empty;
                    var entryObject = api.GetPairValue(entryItem) ?? entryItem;
                    var value = api.GetStringEntryValue(entryObject);

                    if (string.IsNullOrWhiteSpace(value)) continue;

                    result.Entries.Add(new LocalizationEntry
                    {
                        Id = BuildId(nsName, key),
                        SourceText = value,
                        SourceFile = filePath,
                        Context = nsName
                    });
                }
            }

            return result;
        }, cancellationToken);
    }

    public async Task WriteAsync(
        string sourceFilePath,
        IReadOnlyCollection<LocalizationEntry> entries,
        string outputFilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var api = LoadLocresApi();
        var translated = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.TranslatedText))
            .ToDictionary(x => x.Id, x => x.TranslatedText!, StringComparer.Ordinal);

        await Task.Run(() =>
        {
            var fileObject = api.CreateFile();
            using (var input = File.OpenRead(sourceFilePath))
                api.Load(fileObject, input);

            foreach (var nsItem in api.Enumerate(fileObject))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nsName = api.GetPairKey(nsItem) ?? string.Empty;
                var nsValue = api.GetPairValue(nsItem) ?? nsItem;

                foreach (var entryItem in api.Enumerate(nsValue))
                {
                    var key = api.GetPairKey(entryItem) ?? string.Empty;
                    var entryObject = api.GetPairValue(entryItem) ?? entryItem;
                    var id = BuildId(nsName, key);
                    if (translated.TryGetValue(id, out var value))
                        api.SetStringEntryValue(entryObject, value);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
            using var output = File.Create(outputFilePath);
            api.SaveOptimized(fileObject, output);
        }, cancellationToken);
    }

    private static string BuildId(string ns, string key) => $"{ns}::{key}";

    private static LocresReflectionApi LoadLocresApi()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "LocresLib.dll"),
            Path.Combine(AppContext.BaseDirectory, "lib", "LocresLib.dll"),
            Path.Combine(Environment.CurrentDirectory, "LocresLib.dll"),
            Path.Combine(Environment.CurrentDirectory, "lib", "LocresLib.dll")
        };

        var dll = candidates.FirstOrDefault(File.Exists);
        if (dll is null)
        {
            throw new InvalidOperationException(
                "LocresLib.dll bulunamadı. UnrealLocres/LocresLib'i derleyip uygulama klasörüne veya lib klasörüne koyun.");
        }

        return new LocresReflectionApi(Assembly.LoadFrom(dll));
    }

    private sealed class LocresReflectionApi
    {
        private readonly Type _fileType;
        private readonly Type _versionType;
        private readonly MethodInfo _load;
        private readonly MethodInfo _save;

        public LocresReflectionApi(Assembly assembly)
        {
            _fileType = assembly.GetType("LocresLib.LocresFile")
                ?? assembly.GetType("LocresLib.LocResFile")
                ?? throw new InvalidOperationException("LocresLib içinde LocresFile/LocResFile tipi bulunamadı.");

            _versionType = assembly.GetType("LocresLib.LocresVersion")
                ?? assembly.GetType("LocresLib.LocResVersion")
                ?? throw new InvalidOperationException("LocresVersion tipi bulunamadı.");

            _load = _fileType.GetMethod("Load", [typeof(Stream)])
                ?? throw new InvalidOperationException("LocresFile.Load(Stream) bulunamadı.");

            _save = _fileType.GetMethods()
                .FirstOrDefault(m => m.Name == "Save" && m.GetParameters().Length == 2)
                ?? throw new InvalidOperationException("LocresFile.Save(Stream, version) bulunamadı.");
        }

        public object CreateFile() => Activator.CreateInstance(_fileType)!;
        public void Load(object file, Stream stream) => _load.Invoke(file, [stream]);

        public IEnumerable<object> Enumerate(object value)
        {
            if (value is not IEnumerable enumerable) yield break;
            foreach (var item in enumerable)
                if (item is not null) yield return item;
        }

        public string? GetPairKey(object item)
            => item.GetType().GetProperty("Key")?.GetValue(item)?.ToString();

        public object? GetPairValue(object item)
            => item.GetType().GetProperty("Value")?.GetValue(item);

        public string GetStringEntryValue(object entry)
        {
            var property = entry.GetType().GetProperty("Value");
            return property?.GetValue(entry)?.ToString() ?? string.Empty;
        }

        public void SetStringEntryValue(object entry, string value)
        {
            var property = entry.GetType().GetProperty("Value");
            if (property is null || !property.CanWrite)
                throw new InvalidOperationException("Locres string entry Value alanı yazılabilir değil.");
            property.SetValue(entry, value);
        }

        public void SaveOptimized(object file, Stream output)
        {
            object version;
            try
            {
                version = Enum.Parse(_versionType, "Optimized", ignoreCase: false);
            }
            catch
            {
                version = Enum.GetValues(_versionType).GetValue(Enum.GetValues(_versionType).Length - 1)!;
            }
            _save.Invoke(file, [output, version]);
        }
    }
}
