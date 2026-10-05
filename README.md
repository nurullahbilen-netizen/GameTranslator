# Game Translator POC (.NET 8 WPF)

Bu proje Windows oyunlarında **dosya tabanlı yerelleştirme** için güvenli bir POC'tur.

## Mimari

- `GameTranslator.Core`
  - Modeller
  - `ILocalizationExtractor`
  - `ITranslationService`
  - `IGameScanner`
- `GameTranslator.Infrastructure`
  - JSON/XML/CSV extractor/exporter
  - Klasör tarayıcı
  - Mock ve Ollama çeviri servisleri
- `GameTranslator.App`
  - WPF arayüz
  - Klasör seçme, tarama, çıkarma, çeviri, export

## Çalıştırma

Windows'ta .NET 8 SDK kurulu olmalı.

```powershell
dotnet restore
dotnet build GameTranslator.sln
dotnet run --project .\src\GameTranslator.App\GameTranslator.App.csproj
```

## POC kapsamı

- Desteklenen formatlar: `.json`, `.lang`, `.xml`, `.csv`, `.po`
- Çeviri varsayılan olarak `MockTranslationService` kullanır; `[TR]` prefiksi ekler.
- Ollama örneği hazırdır; `MainWindow.xaml.cs` içindeki translator nesnesini değiştirerek kullanılabilir.
- Export, orijinal oyun dosyalarını değiştirmez; `GameTranslator_Output` altına yazar.

## Sonraki modüller

- PO extractor/exporter
- Akıllı string filtreleme
- Placeholder koruması
- Translation cache (SQLite)
- DeepL / Google adapterları
- Unity/Unreal asset pluginleri (oyuna özel, hukuki/teknik uyumluluk kontrolü ile)
- Patch manifest ve rollback
- Profil sistemi (oyun başına parser/encoding/font ayarları)

## Runtime / Hooking notu

Runtime hooking veya process-memory modülü ayrı native plugin olarak tasarlanmalıdır. Bu POC kasıtlı olarak işlem belleğine kod enjekte etmez ve anti-cheat mekanizmalarını atlatmaya çalışmaz.

## V1.1 - Placeholder protection + Unreal/Aion 2 discovery

### PlaceholderProtector
`GameTranslator.Core/Text/PlaceholderProtector.cs` protects runtime tokens before translation and restores them afterwards.
The translation pipeline now wraps the selected provider with `PlaceholderSafeTranslationService`.

Protected examples include:
- `{playerName}`, `{0}`, `{0:N0}`
- `%d`, `%s`, `%1$s`, `%%`
- `\\n`, `\\r`, `\\t`
- `<color=#fff>`, `</color>`, `<b>`
- `[tag]`, `[/tag]`

If the translation engine removes or mutates a protected marker, the decorator fails fast instead of silently writing a broken game string.

### Unreal .locres
`UnrealLocresExtractor` is registered as an `ILocalizationExtractor` for `.locres` files.
To avoid hard-coupling the POC to one third-party implementation, it dynamically loads `LocresLib.dll` at runtime.

Build `LocresLib` from the open-source UnrealLocres project, then copy `LocresLib.dll` to either:

```text
GameTranslator.App/bin/.../LocresLib.dll
```

or:

```text
GameTranslator.App/bin/.../lib/LocresLib.dll
```

The adapter loads the `.locres`, emits IDs in `Namespace::Key` form, and can write translated values into a new `.locres` output file.

### Aion 2 analysis
`GameTranslator.Infrastructure/Games/Aion2/Aion2Extractor.cs` currently performs conservative discovery only. It inventories:
- localization candidates (`.locres`, `.json`, `.xml`, `.csv`, `.po`, `.lang`)
- Unreal archives (`.pak`, `.utoc`, `.ucas`)
- font candidates (`.ttf`, `.otf`, `.ufont`)

It intentionally does **not** bypass encryption, anti-cheat, or protected archives. The next layer should use a dedicated Unreal asset provider (for example a CUE4Parse-based provider) for archives that can be lawfully/readably opened.

## V1.2 - Game Inspector & Report Engine

V1.2 adds a read-only Unreal/Aion 2 inspection pipeline and a WPF dashboard.

### New modules

- `GameTranslator.Core/Inspection/*`
  - `GameInspectionReport`
  - `GameAssetCandidate`
  - engine/archive/recommendation enums
- `GameTranslator.Infrastructure/Games/Unreal/UnrealGameInspector.cs`
  - detects Unreal indicators
  - reads `Engine/Build/Build.version` when available
  - inventories `.pak`, `.utoc`, `.ucas`, `.locres`, fonts and loose localization files
  - identifies loose StringTable/DataTable `.uasset` candidates heuristically
  - recommends `Loose Locres`, `Loose Structured Files`, `CUE4Parse Archive`, or `OCR Fallback`
- `GameTranslator.Infrastructure/Games/Aion2/Aion2Extractor.cs`
  - Aion 2 profile wrapper over the generic Unreal inspector
- WPF `Game Inspector` tab
  - engine/version/confidence cards
  - archive type and encryption status
  - candidate and evidence grids
  - recommendation panel

### Important V1.2 behavior

The inspector is intentionally read-only. It does not decrypt archives, bypass anti-cheat, inject code, or modify game files.

Exact Unreal minor version is only shown when trustworthy metadata such as `Engine/Build/Build.version` is present. IoStore alone is not treated as proof of a specific UE version.

Archive encryption remains `Unknown` until a later archive provider can attempt a normal mount and report whether an AES key is required.

StringTable/DataTable counts in V1.2 cover loose `.uasset` candidates by path/name. Archive-contained objects will be discovered by the planned CUE4Parse provider in V1.3.

---

# V1.3 - CUE4Parse Archive Provider

V1.3 adds a read-only Unreal archive path on top of the V1.2 Inspector.

## New files

```text
GameTranslator.Core/
  Abstractions/
    IArchiveLocalizationProvider.cs
  Inspection/
    ArchiveExtractionResult.cs

GameTranslator.Infrastructure/
  Games/Unreal/Archive/
    Cue4ParseArchiveProvider.cs
```

## NuGet

The Infrastructure project pins **CUE4Parse 1.2.2** because that release targets .NET 8. Newer monthly CUE4Parse builds currently target .NET 10.

```xml
<PackageReference Include="CUE4Parse" Version="1.2.2" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

## V1.3 flow

```text
Game folder
   -> Game Inspector
   -> CUE4Parse header probe
       -> PAK / IoStore encryption state
   -> CUE4Parse archive mount (read-only)
       -> .locres
       -> UStringTable
       -> UDataTable
   -> LocalizationEntry[]
   -> Metinler / Ceviri tab
   -> PlaceholderSafeTranslationService
```

### Encryption

The Inspector probes PAK and UTOC headers through CUE4Parse's public readers and updates `GameInspectionReport.EncryptionState`.

If an archive is encrypted, the UI accepts an **optional user-provided AES key**. V1.3 does not search for, derive, dump, bypass, or brute-force keys. Titles with multiple encryption GUIDs will need a future game-specific key mapping plugin.

### `.locres`

Archive-contained `.locres` bytes are copied to a temporary file and passed to the existing `UnrealLocresExtractor`, then normalized back to `LocalizationEntry` records with an `archive://` source path.

### `UStringTable` / `UDataTable`

CUE4Parse loads package exports. V1.3 detects `UStringTable` and `UDataTable` exports by runtime type name, serializes only those exports, then recursively extracts string leaves. This keeps the provider decoupled from version-sensitive internal property layouts.

This is intentionally a POC extraction strategy. V1.4 should add typed adapters for the exact StringTable/DataTable layouts of the target engine/game and use mappings (`.usmap`) when required.

## GUI

The `Metinler / Ceviri` tab now includes:

- `Loose Metinleri Cikar`
- `Arsivden Metinleri Cikar (CUE4Parse)`
- optional AES key field
- extracted archive text is loaded into the existing translation DataGrid

## Build

```powershell
dotnet restore
dotnet build GameTranslator.sln
dotnet run --project .\src\GameTranslator.App\GameTranslator.App.csproj
```

Aion 2 is not required to compile the architecture. A real Unreal title is required to validate game-specific archive versions, mappings, compression/native dependencies and asset layouts.
