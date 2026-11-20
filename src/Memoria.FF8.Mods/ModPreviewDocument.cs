using System.Globalization;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.TextEncoding;
using Albeoris.Games.FF8.Msd;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public sealed record ModPreviewEntry(string Id, string Category, string Group, string Name)
{
    public string? Folder { get; init; }
    public string? SourcePath { get; init; }
    internal string? TextureMember { get; init; }
    public bool IsMedia => Category == "Audio" || Category == "Video" && Group != "Camera tracks";
}
public sealed record ModPreviewSide(string Text, PixelImage? Image = null)
{
    public EditableModel? Model { get; init; }
    public bool CanUseItem { get; init; }
}

public sealed record ModPreviewTextRow(string Title, string Before, string After);

public sealed record ModPreviewComparison(ModPreviewSide Before, ModPreviewSide After)
{
    public IReadOnlyList<ModPreviewTextRow> TextRows { get; init; } = [];

    public ModPreviewComparison SelectTextRow(int index)
    {
        ModPreviewTextRow row = TextRows[index];
        return this with { Before = new(row.Title + "\n" + row.Before), After = new(row.Title + "\n" + row.After) };
    }
}
public sealed record ModPreviewResourcesProfile(string GameDirectory, string? ModelSourceDirectory = null, string? Language = null)
{
    public Func<FieldAssets, PixelImage>? RenderField { get; init; }
    public Func<FieldAssets, IReadOnlyList<FieldBackgroundPatch>, PixelImage>? RenderFieldPatch { get; init; }
    public Func<FieldAssets, FieldAssets, IReadOnlyList<FieldScenePatch>, Func<int, FieldAssets>, CancellationToken, ModPreviewComparison>? CompareFieldScene { get; init; }
    public string? Ff7GameDirectory { get; init; }
    public Func<FieldAssets, int, int?>? ResolveFieldActorModel { get; init; }
}

public sealed record ModPreviewMediaInput(string Name, string? Path, Func<byte[]>? Read = null)
{
    public Action<Stream>? CopyTo { get; init; }
    public ModFileSegment? Segment { get; init; }
    public string? InstrumentBank { get; init; }
    public ExternalMusicSelection? ExternalMusic { get; init; }
    public Func<Stream, CancellationToken, Task>? WriteWave { get; init; }
}

/// <summary>Keeps mounted original media available until playback and all child decoders close.</summary>
public sealed class ModPreviewMediaSources : IDisposable
{
    private IResourceSource? source;

    internal ModPreviewMediaSources(IResourceSource source, ModPreviewMediaInput? before, ModPreviewMediaInput after, bool video)
    {
        this.source = source;
        Before = before;
        After = after;
        Video = video;
    }

    public ModPreviewMediaInput? Before { get; }
    public ModPreviewMediaInput After { get; }
    public bool Video { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref source, null)?.Dispose();
    }
}

/// <summary>Browse a single checked package. The selected installation supplies the unmodified resources.</summary>
public sealed class ModPreviewDocument
{
    private const long MaximumSampleBytes = 64 * 1024 * 1024;
    private readonly ModCatalogSnapshot catalog;
    private readonly IReadOnlyDictionary<string, ModFile> files;
    private readonly JunctionManifestMetadata? junction;
    private readonly Lazy<LocalizationProject> localization;
    private readonly SemaphoreSlim comparisonGate = new(1, 1);
    private const string FieldScenePrefix = "scene:field/";

    private ModPreviewDocument(ModPackage package, List<ModFile> files, JunctionManifestMetadata? junction, ModCatalogSnapshot? preferences)
    {
        Name = package.Name;
        package = package with
        {
            Status = ModEffectiveStatus.Active,
            HasLocalization = package.HasLocalization || files.Any(file => file.RelativePath.Equals(LocalizationProjectIO.ManifestRelativePath, StringComparison.OrdinalIgnoreCase))
        };
        catalog = new(0, [package], files)
        {
            StatusPreferences = preferences?.StatusPreferences ?? System.Collections.Immutable.ImmutableDictionary<string, int>.Empty,
            Hardcore = preferences?.Hardcore == true
        };
        this.files = files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
        this.junction = junction;
        localization = new(() => LocalizationProjectIO.ReadForAuthoring(package.RootDirectory));
        List<ModPreviewEntry> entries = files.Where(file => !file.RelativePath.EndsWith(TextureModMapping.Extension, StringComparison.OrdinalIgnoreCase)).SelectMany(file => GetEntries(file, junction?.VideoExtension)).ToList();
        entries.AddRange(ModPreviewResources.DescribeBundledGameplay(package));
        entries.AddRange(ModPreviewResources.DescribeFieldGameplay(catalog));
        if (package.HasLocalization)
        {
            entries.Add(new(LocalizationSceneId, "Text", "Playthrough", "Play from the beginning"));
        }
        if (entries.Any(entry => entry.Category == "World map" || entry.Id.StartsWith("models/world/", StringComparison.OrdinalIgnoreCase)))
        {
            entries.Add(new(WorldSceneId, "World map", "Explore", "World map comparison"));
        }
        foreach (var field in entries.Where(entry => entry.Category == "Fields" && entry.Group.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')).GroupBy(entry => entry.Group).Where(group => group.Count() > 1).ToArray())
        {
            entries.Add(new(FieldScenePrefix + field.Key, "Fields", field.Key, "Compare entire field"));
        }
        if (junction is not null)
        {
            entries.AddRange(junction.Behaviors.Select(behavior => new ModPreviewEntry("behavior:" + behavior, "Gameplay", "Reviewed patches", Behavior(behavior).Name)));
            entries.AddRange(junction.GameplayOverrides.Select(change => new ModPreviewEntry("rule:" + change.Setting, "Gameplay", "Reviewed patches", Gameplay(change).Name)));
        }
        Entries = Array.AsReadOnly(entries.OrderBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Group, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Id, StringComparer.Ordinal).ToArray());
        Tree = ModPreviewTree.Create(Entries, this.files.Keys);
    }

    public string Name { get; }
    public IReadOnlyList<ModPreviewEntry> Entries { get; }
    public ModPreviewTree Tree { get; }
    public ModCatalogSnapshot PackageCatalog => catalog;
    public const string WorldSceneId = "scene:world";
    public const string LocalizationSceneId = "scene:localization";

    public static bool CanOpenInstalled(ModPackage? package) => package is { Format: ModFormat.Memoria or ModFormat.Junction or ModFormat.Demaster, IsSettingsGroup: false };

    internal static ModPreviewDocument Open(ModPackage package, CancellationToken cancellationToken, bool useInstalledOptions = true, ModCatalogSnapshot? preferences = null)
    {
        List<ModFile> files = [];
        JunctionManifestMetadata? junction = null;
        if (package.Format == ModFormat.Junction)
        {
            string ini = Directory.Exists(package.RootDirectory) ? Path.Combine(package.RootDirectory, "mod.ini") : package.RootDirectory + ".ini";
            JunctionModPackage imported = JunctionModImporter.Open(package.RootDirectory, useInstalledOptions ? ModConfiguration.ReadJunctionOptions(ini) : null);
            junction = imported.Manifest;
            package = package with { JunctionMetadata = junction };
            bool folder = Directory.Exists(package.RootDirectory);
            foreach (JunctionReplacement file in imported.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] Read()
                {
                    using Stream stream = file.OpenRead();
                    return ReadSample(stream, file.Length);
                }
                string? absolute = folder ? Path.Combine(package.RootDirectory, file.SourcePath) : null;
                files.Add(new(package.Id, package.Format, file.RelativePath, files.Count, Read, false, file.CopyTo, () => file.FileSegment) { AbsolutePath = absolute });
            }
        }
        else if (package.Format == ModFormat.Memoria)
        {
            foreach (string path in ModPackageInstaller.SafeFiles(package.RootDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = Path.GetRelativePath(package.RootDirectory, path).Replace('\\', '/');
                if (IsPackageMetadata(relative))
                {
                    continue;
                }
                files.Add(CreatePreviewFile(package, path, relative, files.Count));
            }
        }
        else if (package.Format == ModFormat.Demaster)
        {
            List<(string Physical, string Relative)> discovered = [];
            ModRuntime.EnumerateFiles(package.RootDirectory, package.RootDirectory, string.Empty, package.Id, new(StringComparer.OrdinalIgnoreCase), discovered, [], cancellationToken: cancellationToken);
            var selected = discovered.DistinctBy(file => file.Relative, StringComparer.OrdinalIgnoreCase).Select(file => (package.RootDirectory, file.Physical, file.Relative)).ToList();
            var layouts = ModRuntime.ReadTextureLayouts(package.Id, package.Format, selected).Entries;
            foreach (var (_, path, relative) in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TextureModPaths.IsImage(relative))
                {
                    files.Add(CreatePreviewFile(package, path, relative, files.Count, layouts.GetValueOrDefault(relative)));
                }
            }
        }
        else
        {
            throw new NotSupportedException("This package format does not support comparison.");
        }
        return new(package, files, junction, preferences);
    }

    private static ModFile CreatePreviewFile(ModPackage package, string path, string relative, long revision, TextureLayoutMetadata? textureLayout = null)
    {
        FileInfo info = new(path);
        long length = info.Length;
        long stamp = info.LastWriteTimeUtc.Ticks;
        byte[] Read()
        {
            if (package.Format == ModFormat.Demaster)
            {
                ModRuntime.ValidateContentPath(package.RootDirectory, path);
            }
            using FileStream stream = File.OpenRead(path);
            if (stream.Length != length || File.GetLastWriteTimeUtc(path).Ticks != stamp)
            {
                throw new IOException("The mod changed on disk. Reopen its preview to compare the current version.");
            }
            return ReadSample(stream, length);
        }
        return new(package.Id, package.Format, relative, revision, Read, cacheContent: false) { AbsolutePath = path, ContentStamp = (length, stamp), TextureLayout = textureLayout };
    }

    public Task<ModPreviewComparison> CompareAsync(ModPreviewEntry entry, ModPreviewResourcesProfile profile, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            await comparisonGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return Compare(entry, profile, cancellationToken);
            }
            finally
            {
                comparisonGate.Release();
            }
        }, cancellationToken);
    }

    private ModPreviewComparison Compare(ModPreviewEntry entry, ModPreviewResourcesProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entry.Id.StartsWith(FieldScenePrefix, StringComparison.Ordinal) && profile.RenderField is { } renderField)
        {
            return CompareField(entry.Id[FieldScenePrefix.Length..], profile, renderField, cancellationToken);
        }
        if (entry.Id.StartsWith("behavior:", StringComparison.Ordinal) && Enum.TryParse(entry.Id.AsSpan(9), out JunctionBehavior behavior) && junction?.Behaviors.Contains(behavior) == true)
        {
            var detail = Behavior(behavior);
            return new(new(detail.Before), new(detail.After));
        }
        if (entry.Id.StartsWith("rule:", StringComparison.Ordinal) && Enum.TryParse(entry.Id.AsSpan(5), out JunctionGameplaySetting setting) && junction?.GameplayOverrides.FirstOrDefault(change => change.Setting == setting) is { } rule)
        {
            var detail = Gameplay(rule);
            return new(new(detail.Before), new(detail.After));
        }
        files.TryGetValue(entry.SourcePath ?? entry.Id, out ModFile? file);
        if (ModPreviewResources.CompareGameplay(catalog, entry, file, profile, cancellationToken) is { } gameplay)
        {
            return gameplay;
        }
        if (file is null)
        {
            throw new ArgumentException("The selected change does not belong to this package.", nameof(entry));
        }
        if (file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("fields/", StringComparison.OrdinalIgnoreCase) && (profile.CompareFieldScene is not null || profile.RenderFieldPatch is not null))
        {
            return CompareFieldPatch(file, profile, cancellationToken);
        }
        if (entry.Category == "Text" && entry.Group == "Localization")
        {
            return TextureModPaths.IsImage(file.RelativePath)
                ? CompareLocalizationArtwork(file, profile, cancellationToken)
                : CompareLocalization(file, cancellationToken);
        }
        if (ItemOverrideMods.IsItemDocument(file))
        {
            return CompareItem(file, profile);
        }
        if (entry.Category == "Models" && !TextureModPaths.IsImage(file.RelativePath))
        {
            ModPreviewComparison? model = file.Format == ModFormat.Memoria ? CompareModel(file, profile, cancellationToken) : CompareJunctionModel(file, profile, cancellationToken);
            if (model is not null)
            {
                return model;
            }
        }
        if (file.Format == ModFormat.Junction && entry is { Category: "Video", Group: "Camera tracks" })
        {
            return CompareMovieCamera(file, profile, cancellationToken);
        }
        if (TextureModPaths.IsImage(file.RelativePath))
        {
            if (ModPreviewResources.CompareTexture(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language, catalog, file.RelativePath, cancellationToken, profile.RenderField, entry.TextureMember) is { } texture)
            {
                return texture;
            }
            if (entry.TextureMember is not null)
            {
                return new(new("Original asset could not be matched to this atlas entry."), new(entry.Name));
            }
            return new(new("Original asset could not be matched to this file."), new(entry.Name, ModImageDecoder.Decode(file).Image));
        }
        if (file.Format == ModFormat.Junction && JunctionEncodedResources.Address(file.RelativePath) is { } key && key.Path.EndsWith(".msd", StringComparison.OrdinalIgnoreCase))
        {
            return CompareFieldText(file, profile, key);
        }
        if (file.Format == ModFormat.Junction && profile.RenderField is { } render && JunctionEncodedResources.Address(file.RelativePath) is { } fieldKey && JunctionEncodedResources.FieldGroup(fieldKey) is { } field)
        {
            return CompareField(field, profile, render, cancellationToken);
        }
        return new(new("Original resource preview unavailable."), new($"{file.RelativePath}\nThis resource type does not have a comparison view."));
    }

    private ModPreviewComparison CompareFieldPatch(ModFile file, ModPreviewResourcesProfile profile, CancellationToken cancellationToken)
    {
        if (!FieldSceneMods.TryPrepare(catalog, out FieldSceneMods scenes, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
        }
        FieldScenePatch patch = scenes.Patches.Single(owned => owned.RelativePath == file.RelativePath).Patch;
        using IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        using GameAssets assets = new(profile.GameDirectory, resources: new(source, profile.Language));
        FieldAssets field = assets.LoadField(patch.FieldId);
        if (!field.Name.Equals(patch.FieldName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The field patch does not match the selected installation.");
        }
        if (profile.CompareFieldScene is { } compare)
        {
            return compare(field, field, [patch], assets.LoadField, cancellationToken);
        }
        var render = profile.RenderFieldPatch!;
        List<string> effects = [field.Name];
        if (patch.Backgrounds.Count != 0)
        {
            effects.Add($"{patch.Backgrounds.Count} background changes");
        }
        if (patch.Actors.Count != 0)
        {
            effects.Add($"{patch.Actors.Count} actor appearance changes during play");
        }
        if (patch.Movement is { } movement)
        {
            effects.Add($"Movement rotation {movement.RotationDegrees:g}°");
        }
        return new(new(field.Name, render(field, [])), new(string.Join(" · ", effects), render(field, patch.Backgrounds)));
    }

    public Task<ModPreviewMediaSources> OpenMediaAsync(ModPreviewEntry entry, ModPreviewResourcesProfile profile, CancellationToken cancellationToken)
    {
        if (!entry.IsMedia || !files.TryGetValue(entry.Id, out ModFile? file))
        {
            throw new ArgumentException("Select an audio or video resource from this package.", nameof(entry));
        }
        return Task.Run(() => ResolveMedia(file, profile, entry.Category == "Video", cancellationToken), cancellationToken);
    }

    private ModPreviewMediaSources ResolveMedia(ModFile file, ModPreviewResourcesProfile profile, bool video, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string name = Path.GetFileName(file.RelativePath);
        string stem = Path.GetFileNameWithoutExtension(name);
        IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        try
        {
            ResourceService resources = new(source, profile.Language);
            IGameFileSource? original = resources.GetSource<IGameFileSource>();
            List<string> candidates = [file.RelativePath];
            ExternalMusicSelection? external = null;
            if (video)
            {
                candidates.AddRange([$"movies/{stem}.avi", $"movies/{stem}h.avi", $"movies/{name}"]);
            }
            else
            {
                candidates.AddRange([$"music/stream/{name}", $"music/stream/{stem}.wav", $"music/dmusic/{name}"]);
                if (junction is not null)
                {
                    external = AddNativeMusicPaths(resources, file, candidates, token);
                }
            }
            ModPreviewMediaInput? before = null;
            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if (FindFile(candidate) is { } path)
                {
                    before = new(Path.GetFileName(candidate), path);
                    break;
                }
            }
            string? bank = !video && (IsSegment(name) || before?.Path is { } originalPath && IsSegment(originalPath))
                ? FindFile("music/dmusic/ff8.dls")
                : null;
            if (before is not null)
            {
                before = before with { InstrumentBank = bank };
            }
            token.ThrowIfCancellationRequested();
            ModPreviewMediaInput after = new(name, file.AbsolutePath, file.ReadBytes)
            {
                CopyTo = file.CopyTo,
                Segment = file.ResolveFileSegment(),
                InstrumentBank = bank,
                ExternalMusic = external
            };

            string? FindFile(string relative)
            {
                try
                {
                    string? physical = original?.ResolveFile(relative);
                    return File.Exists(physical) ? physical : null;
                }
                catch (FileNotFoundException)
                {
                    // Remastered archives throw for an absent member, while loose sources return its path.
                    return null;
                }
            }
            return new(source, before, after, video);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    private static bool IsSegment(string path) => Path.GetExtension(path).Equals(".sgt", StringComparison.OrdinalIgnoreCase);

    private static ModPreviewComparison CompareMovieCamera(ModFile file, ModPreviewResourcesProfile profile, CancellationToken token)
    {
        string name = Path.GetFileName(file.RelativePath);
        using IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        IGameFileSource original = new ResourceService(source, profile.Language).GetSource<IGameFileSource>()
            ?? throw new NotSupportedException("The installation cannot resolve the original movie camera.");
        byte[] beforeBytes = File.ReadAllBytes(original.ResolveFile("movies/" + name));
        byte[] afterBytes = file.ReadBytes();
        NativeMovieCameraFlags before = NativeMovieCameraReader.Read(beforeBytes);
        NativeMovieCameraFlags after = NativeMovieCameraReader.ReadPresentation(afterBytes);
        List<ModPreviewTextRow> rows = [];
        for (int index = 0; index < Math.Max(before.Flags.Length, after.Flags.Length); index++)
        {
            token.ThrowIfCancellationRequested();
            FieldCamera? originalCamera = ReadCamera(beforeBytes, before, index);
            FieldCamera? changedCamera = ReadCamera(afterBytes, after, index);
            int? beforeFlags = index < before.Flags.Length ? before.Flags[index] : null;
            int? afterFlags = index < after.Flags.Length ? after.Flags[index] : null;
            if (originalCamera != changedCamera || beforeFlags != afterFlags)
            {
                rows.Add(new($"Camera record {index + 1}", Describe(originalCamera, beforeFlags), Describe(changedCamera, afterFlags)));
            }
        }
        ModPreviewComparison comparison = new(new($"{before.Flags.Length:N0} original camera records"), new($"{after.Flags.Length:N0} package camera records\n{rows.Count:N0} changed records"))
        {
            TextRows = rows
        };
        return rows.Count == 0 ? comparison : comparison.SelectTextRow(0);

        static FieldCamera? ReadCamera(byte[] input, NativeMovieCameraFlags flags, int index)
        {
            return index < flags.Flags.Length ? NativeMovieCameraReader.ReadCamera(input, index) : null;
        }

        static string Describe(FieldCamera? camera, int? flags)
        {
            if (camera is null)
            {
                return "No camera record.";
            }
            return $"Position {camera.Translation}\nPan {camera.Pan} · Focal length {camera.FocalLength:G}\nOrientation {camera.AxisX} / {camera.AxisY} / {camera.AxisZ}\nField compositing flags 0x{flags:X2}";
        }
    }

    private ExternalMusicSelection? AddNativeMusicPaths(ResourceService resources, ModFile file, List<string> candidates, CancellationToken token)
    {
        ExternalMusicResolver resolver = new(catalog);
        NativeMusicCatalog music;
        try
        {
            music = new(resources);
        }
        catch (NotSupportedException)
        {
            // Loose streams remain available in installations without an authenticated music table.
            return resolver.Resolve(file.RelativePath);
        }
        ExternalMusicSelection? selected = null;
        for (int track = 0; track < 117; track++)
        {
            token.ThrowIfCancellationRequested();
            string native;
            try
            {
                native = music.MusicFileResolved(track).Replace('\\', '/');
            }
            catch (InvalidDataException)
            {
                continue;
            }
            ExternalMusicSelection? match = resolver.Resolve(native);
            if (match?.File != file)
            {
                match = resolver.Resolve(native, noIntro: true);
            }
            if (match?.File == file && native.StartsWith("Data/", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Insert(0, native[5..]);
                selected ??= match;
            }
        }
        return selected ?? resolver.Resolve(file.RelativePath);
    }

    private ModPreviewComparison CompareLocalization(ModFile file, CancellationToken cancellationToken)
    {
        LocalizationProject project = localization.Value;
        LocalizationPayload? payload = LocalizationProjectIO.DiscoverPayloads(catalog.Packages[0].RootDirectory, project.Manifest)
            .FirstOrDefault(item => item.Path.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase));
        if (payload is null && !file.RelativePath.Equals(LocalizationProjectIO.ManifestRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return new(new("Original localization metadata"), new("This package file supports the translation. Open a text payload or play from the beginning to see its changes."));
        }
        List<ModPreviewTextRow> rows = [];
        foreach (LocalizationRecord row in project.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(row.Target) && row.Target != row.Source
                && (payload?.Category is null || row.Category == payload.Category)
                && (payload?.File is null || row.File == payload.File))
            {
                rows.Add(new($"{row.File} · {row.Key}", row.Source ?? "—", row.Target));
            }
        }
        return CompareTextRows(rows);
    }

    private ModPreviewComparison CompareLocalizationArtwork(ModFile file, ModPreviewResourcesProfile profile, CancellationToken token)
    {
        LocalizationArtworkFile? artwork = localization.Value.Artwork.FirstOrDefault(item =>
            item.Declaration.TargetImage?.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase) == true
            || item.Declaration.SourceImage.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase));
        if (artwork is null || artwork.TargetPng is null)
        {
            ModPreviewSide source = new("Reference artwork", ModImageDecoder.Decode(file).Image);
            return new(source, new("This file has no translated artwork."));
        }
        token.ThrowIfCancellationRequested();
        using IResourceSource sourceResources = GameDataSourceFactory.OpenTextureAuthoring(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        ResourceService resources = new(sourceResources, profile.Language);
        using GameAssets assets = new(profile.GameDirectory, resources: resources);
        TextureKey key = artwork.Declaration.Key;
        TextureTarget target = new("ff8", key.Owner.Path, key.Subresource, key.Palette);
        TextureCatalogEntry? entry = assets.FindCatalogTextureTarget(target, token);
        PixelImage original = entry is null
            ? ModImageDecoder.Decode(artwork.SourcePng, artwork.Declaration.SourceImage).Image
            : resources.ResolveOriginalForExport(assets.LoadCatalogTexture(entry)).Image;
        return new(new(entry is null ? "Source artwork recorded by the translation" : key.Owner.Path, original),
            new(key.Owner.Path, ModImageDecoder.Decode(artwork.TargetPng, artwork.Declaration.TargetImage!).Image));
    }

    private static ModPreviewComparison CompareTextRows(IReadOnlyList<ModPreviewTextRow> rows)
    {
        ModPreviewComparison difference = new(new("No text changes."), new("No text changes.")) { TextRows = rows };
        return rows.Count == 0 ? difference : difference.SelectTextRow(0);
    }

    private ModPreviewComparison CompareField(string field, ModPreviewResourcesProfile profile, Func<FieldAssets, PixelImage> render, CancellationToken cancellationToken)
    {
        HashSet<string> paths = new(Entries.Where(entry => entry.Category == "Fields" && entry.Group == field).Select(entry => entry.Id), StringComparer.OrdinalIgnoreCase);
        HashSet<string?> imageDirectories = new(paths.Where(TextureModPaths.IsImage).Select(Path.GetDirectoryName), StringComparer.OrdinalIgnoreCase);
        ModCatalogSnapshot subset = new(catalog.Generation, catalog.Packages, catalog.Files.Where(file => paths.Contains(file.RelativePath)
            || file.RelativePath.EndsWith(TextureModMapping.Extension, StringComparison.OrdinalIgnoreCase) && imageDirectories.Contains(Path.GetDirectoryName(file.RelativePath))
            || JunctionEncodedResources.Address(file.RelativePath) is { } key && JunctionEncodedResources.FieldGroup(key) == field));
        if (!JunctionEncodedResources.TryPrepare(subset, out IReadOnlyList<EncodedResource> replacements, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
        }
        if (!NativeTextureMods.TryPrepare(subset, out NativeTextureMods textures, out diagnostics, cancellationToken))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
        }
        if (!FieldSceneMods.TryPrepare(subset, out FieldSceneMods scenes, out diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
        }
        using IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        ResourceService resources = new(source, profile.Language);
        using GameAssets original = new(profile.GameDirectory, resources: resources);
        int id = original.FindCatalogFieldId(field);
        if (id < 0)
        {
            return new(new("This field is unavailable in the selected game resources."), new(field));
        }
        FieldAssets before = original.LoadField(id);
        cancellationToken.ThrowIfCancellationRequested();
        using IResourceSource modifiedSource = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        ResourceService modifiedResources = new(modifiedSource, profile.Language);
        ITextureReplacementResolver legacy = subset.Packages[0].Format == ModFormat.Demaster ? new DemasterTextureResolver(subset) : new FfnxTextureResolver(subset);
        modifiedResources.Prepare(new OrderedTextureResolver(textures, legacy), encodedResources: replacements).Commit();
        using GameAssets modified = new(profile.GameDirectory, resources: modifiedResources);
        FieldAssets applied = modified.LoadField(id);
        FieldScenePatch[] patches = scenes.Patches.Where(owned => owned.Patch.FieldId == id).Select(owned => owned.Patch).ToArray();
        if (patches.Length != 0 && profile.CompareFieldScene is { } compare)
        {
            return compare(before, applied, patches, modified.LoadField, cancellationToken);
        }
        FieldBackgroundPatch[] backgrounds = patches.SelectMany(patch => patch.Backgrounds).ToArray();
        return new(new(field, render(before)), new(field, backgrounds.Length != 0 && profile.RenderFieldPatch is { } renderPatches ? renderPatches(applied, backgrounds) : render(applied)));
    }

    private ModPreviewComparison CompareItem(ModFile document, ModPreviewResourcesProfile profile)
    {
        using IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        ResourceService resources = new(source, profile.Language);
        byte[] items = resources.Read(new("menu/mitem.bin"));
        // The game validates the same declaration and resolves its image by package-relative path.
        if (!ItemOverrideMods.TryPrepare(catalog, items.Length / 4, out ItemOverrideMods overrides, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
        }
        using JsonDocument json = JsonDocument.Parse(document.ReadBytes());
        int id = json.RootElement.GetProperty("id").GetInt32();
        ItemOverride change = overrides.Find(id)!;
        KernelData kernel = KernelData.ReadFromArray(resources.Read(new("main/kernel.bin")), NativeTextEncoding.Create(resources.Source.Language));
        string label = (id < kernel.BattleItems.Count ? kernel.BattleItems[id].Name : kernel.FieldItems.ElementAtOrDefault(id - kernel.BattleItems.Count)?.Name) ?? $"Item {id}";
        NativeFieldItem original = new(items[id * 4], items[id * 4 + 1], items[id * 4 + 2], items[id * 4 + 3]);
        bool nativeUse = original.SupportsCharacterUse || original.SupportsGuardianForceUse || original.SupportsRestUse || original.SupportsNameUse || original.SupportsBlueMagicUse || original.SupportsBattleUse || original.SupportsInventoryUse || original.Type == (byte)ItemType.Magazine;
        string originalText = label + "\n" + (nativeUse ? "Original inventory action" : "Use unavailable");
        string changedText = label + "\n" + (change.Use is not null ? "Use opens the package image." : change.Type == ItemType.Magazine ? "Magazine" : "Original inventory action");
        return new(new(originalText), new(changedText, change.Use is { } use ? ModImageDecoder.Decode(use.Image).Image : null) { CanUseItem = change.Use is not null });
    }

    private ModPreviewComparison? CompareJunctionModel(ModFile file, ModPreviewResourcesProfile profile, CancellationToken cancellationToken)
    {
        if (JunctionEncodedResources.Address(file.RelativePath) is not { } key)
        {
            return null;
        }
        bool modelLibrary = key.Path.StartsWith("field/main_chr/", StringComparison.Ordinal);
        if (!modelLibrary && !key.Path.StartsWith("battle/", StringComparison.Ordinal))
        {
            return null;
        }
        ModCatalogSnapshot selected = new(catalog.Generation, catalog.Packages, [file]);
        if (!JunctionEncodedResources.TryPrepare(selected, out IReadOnlyList<EncodedResource> encoded, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
        }
        using IResourceSource beforeSource = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        using GameAssets beforeAssets = new(profile.GameDirectory, resources: new(beforeSource, profile.Language));
        ModelCatalogEntry? entry = modelLibrary
            ? beforeAssets.EnumerateFieldModelSources(cancellationToken).FirstOrDefault(model => StringComparer.OrdinalIgnoreCase.Equals(model.ModelLibraryResource, key.Path))
            : beforeAssets.EnumerateModelCatalog(cancellationToken).FirstOrDefault(model => model.ResourcePath.Equals(key.Path, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }
        using IResourceSource afterSource = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        ResourceService afterResources = new(afterSource, profile.Language);
        afterResources.Prepare(null, encodedResources: encoded).Commit();
        using GameAssets afterAssets = new(profile.GameDirectory, resources: afterResources);
        return new(new(entry.Name) { Model = beforeAssets.LoadCatalogModel(entry) }, new(entry.Name) { Model = afterAssets.LoadCatalogModel(entry) });
    }

    private ModPreviewComparison? CompareModel(ModFile file, ModPreviewResourcesProfile profile, CancellationToken cancellationToken)
    {
        if (!ModelMods.TryPrepare(catalog, out ModelMods models, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
        }
        ModelModVariant? variant = models.Variants.FirstOrDefault(variant => variant.RelativePath.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase));
        if (variant is null)
        {
            return null;
        }
        using IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        using GameAssets assets = new(profile.GameDirectory, resources: new(source, profile.Language));
        ModelCatalogEntry? entry = assets.EnumerateModelCatalog(cancellationToken).FirstOrDefault(model => model.Id.Equals(variant.ModelId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }
        EditableModel original;
        if (variant.Bank == "embedded")
        {
            original = assets.LoadCatalogModel(entry);
        }
        else
        {
            var bank = assets.DiscoverModelAssets(entry, cancellationToken).FirstOrDefault(asset => asset.Bank == variant.Bank && asset.Load is not null);
            if (bank?.Load is not { } load)
            {
                return null;
            }
            original = load();
        }
        return new(new(entry.Name) { Model = original }, new(entry.Name) { Model = models.Load(variant, original) });
    }

    private static ModPreviewComparison CompareFieldText(ModFile file, ModPreviewResourcesProfile profile, ResourceKey key)
    {
        using IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        ResourceService resources = new(source, profile.Language);
        string field = Path.GetFileNameWithoutExtension(key.Path);
        var encoding = NativeTextEncoding.CreateField(resources.Source.Language, field);
        IReadOnlyList<string> before = MsdFile.Read(resources.Read(key), encoding).Texts;
        IReadOnlyList<string> after = MsdFile.Read(file.ReadBytes(), encoding).Texts;
        ModPreviewTextRow[] rows = Enumerable.Range(0, Math.Max(before.Count, after.Count))
            .Where(index => !string.Equals(before.ElementAtOrDefault(index), after.ElementAtOrDefault(index), StringComparison.Ordinal))
            .Select(index => new ModPreviewTextRow($"Message {index}", before.ElementAtOrDefault(index) ?? "—", after.ElementAtOrDefault(index) ?? "—"))
            .ToArray();
        return CompareTextRows(rows);
    }

    private static bool IsPackageMetadata(string path)
    {
        if (path.Contains('/'))
        {
            return false;
        }
        string stem = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return path.Equals("mod.json", StringComparison.OrdinalIgnoreCase) || path.Equals("mod.ini", StringComparison.OrdinalIgnoreCase) || stem is "readme" or "license" or "notice";
    }

    private static IEnumerable<ModPreviewEntry> GetEntries(ModFile file, string? junctionVideoExtension)
    {
        ModPreviewEntry entry = GetEntry(file, junctionVideoExtension);
        IReadOnlyList<string> members = ModPreviewResources.DescribeTextureAtlasMembers(file);
        if (members.Count == 0 || members is ["atlas"] or ["background"] or ["board-background"])
        {
            yield return entry;
            yield break;
        }
        foreach (string member in members)
        {
            yield return new($"atlas:{entry.Id}#{member}", entry.Category, entry.Group, ModPreviewResources.DescribeTextureAtlasMember(member))
            {
                Folder = Path.GetFileName(entry.Id),
                SourcePath = entry.Id,
                TextureMember = member
            };
        }
    }

    private static ModPreviewEntry GetEntry(ModFile file, string? junctionVideoExtension)
    {
        string path = file.RelativePath;
        if (ModPreviewResources.DescribeGameplayPath(file) is { } gameplay)
        {
            return gameplay;
        }
        if (ModPreviewResources.DescribeTexturePath(file) is { } texture)
        {
            return new(path, texture.Category, texture.Group, texture.Name);
        }
        string[] parts = path.Split('/');
        string extension = Path.GetExtension(path).ToLowerInvariant();
        string name = parts[^1];
        string root = parts[0].ToLowerInvariant();
        ResourceKey? address = file.Format == ModFormat.Junction ? JunctionEncodedResources.Address(path) : null;
        string[] target = (address?.Path ?? path).Split('/');
        string owner = target[0].ToLowerInvariant();
        bool moviePath = root == "movies" || (parts.Length == 1 && name.StartsWith("disc", StringComparison.OrdinalIgnoreCase));
        (string category, string group) = (root, owner, extension) switch
        {
            ("items", _, _) => ("Inventory", "Items"),
            ("models", _, _) => ("Models", parts.Length > 2 ? parts[1] : "Models"),
            ("fields", _, ".json") => ("Fields", Path.GetFileNameWithoutExtension(name)),
            ("localization" or "translations", _, _) => ("Text", "Localization"),
            (_, "field", ".msd") => ("Text", "Field dialogue"),
            (_, "field", ".mch") => ("Models", "Field models"),
            (_, "field", _) => ("Fields", address is { } fieldKey ? JunctionEncodedResources.FieldGroup(fieldKey) ?? "Field resources" : "Field resources"),
            (_, "world", _) => ("World map", "World resources"),
            (_, _, ".cam") when file.Format == ModFormat.Junction && moviePath => ("Video", "Camera tracks"),
            (_, _, _) when file.Format == ModFormat.Junction && moviePath && junctionVideoExtension is not null && extension.Equals("." + junctionVideoExtension, StringComparison.OrdinalIgnoreCase) => ("Video", "Movies"),
            ("music", _, _) => ("Audio", "Music"),
            ("voice", _, _) => ("Audio", "Voices"),
            (_, _, ".ogg" or ".wav" or ".flac" or ".mp3" or ".opus" or ".sgt" or ".psf" or ".minipsf") => ("Audio", "Tracks"),
            (_, _, ".avi" or ".mp4" or ".webm" or ".mkv" or ".bik" or ".mov" or ".wmv" or ".mpg" or ".mpeg" or ".m4v") => ("Video", "Movies"),
            (_, _, ".png" or ".dds") => ("Textures", TextureGroup(parts)),
            (_, _, ".mch" or ".dat") when owner is "field" or "battle" or "world" => ("Models", owner),
            ("statuses" or "encounters" or "abilities" or "gameplay" or "difficulty", _, _) => ("Gameplay", root),
            (_, _, ".txt" or ".csv" or ".xml") => ("Text", "Package files"),
            _ => ("Resources", parts.Length > 1 ? parts[0] : "Package files")
        };
        return new(path, category, group, name);
    }

    private static string TextureGroup(string[] parts)
    {
        if (parts.Any(part => part.Equals("world", StringComparison.OrdinalIgnoreCase) || part.Equals("wm", StringComparison.OrdinalIgnoreCase)))
        {
            return "World map";
        }
        return parts.Length > 2 ? parts[1] : "Artwork";
    }

    private static byte[] ReadSample(Stream stream, long? length)
    {
        if (length > MaximumSampleBytes)
        {
            throw new InvalidDataException("This resource exceeds the limit for an individual comparison.");
        }
        using MemoryStream result = new();
        byte[] buffer = new byte[64 * 1024];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            if (result.Length + read > MaximumSampleBytes)
            {
                throw new InvalidDataException("This resource exceeds the limit for an individual comparison.");
            }
            result.Write(buffer, 0, read);
        }
        return result.ToArray();
    }

    private static (string Name, string Before, string After) Behavior(JunctionBehavior behavior) => behavior switch
    {
        JunctionBehavior.SkipPublisherMovie => ("Publisher movie", "The publisher movie plays at startup.", "The publisher movie is skipped automatically."),
        JunctionBehavior.SkipSquareLogo => ("Square logo", "The Square logo appears at startup.", "The Square logo is skipped automatically."),
        JunctionBehavior.SkipCreditsPause => ("Credits pauses", "The startup credits include pauses.", "The startup credits advance without the pauses."),
        JunctionBehavior.SkipCreditsImages => ("Credits images", "The startup credits display their images.", "The credits images are skipped."),
        JunctionBehavior.InstantManualSkip => ("Manual startup skip", "The startup presentation can be skipped immediately.", "The startup presentation can be skipped immediately."),
        JunctionBehavior.SaveAnywhere => ("Save availability", "Memoria enables saving at safe gameplay boundaries.", "Memoria enables saving at safe gameplay boundaries."),
        JunctionBehavior.DreamJunctionCarry => ("Dream sequence junctions", "The engine carries junctions during supported dream transitions.", "The engine carries junctions during supported dream transitions."),
        _ => throw new NotSupportedException($"Unknown reviewed behavior {behavior}.")
    };

    private static (string Name, string Before, string After) Gameplay(JunctionGameplayOverride change)
    {
        string value = change.Value.ToString(CultureInfo.CurrentCulture);
        return change.Setting switch
        {
            JunctionGameplaySetting.DrawBonus => ("Draw yield", "Standard Draw calculation.", $"Adds {value} to the Draw calculation, within the engine's limits."),
            JunctionGameplaySetting.ApMultiplier => ("Ability points", "Standard AP awards.", $"Multiplies AP awards by {value}."),
            JunctionGameplaySetting.ExperienceMultiplier => ("Experience", "Standard party experience awards.", $"Multiplies party experience awards by {value}."),
            JunctionGameplaySetting.GuardianExperienceMultiplier => ("GF experience", "Standard GF experience awards.", $"Multiplies GF experience awards by {value}."),
            JunctionGameplaySetting.UnlimitedMagic => ("Magic stock", "Supported casts and actions consume magic stock.", "Casting and supported uses preserve magic stock."),
            JunctionGameplaySetting.MaximumDamage => ("Damage limit", "Uses the standard damage limit.", $"Sets the maximum damage to {value}."),
            JunctionGameplaySetting.RareItemFirst => Drop(1),
            JunctionGameplaySetting.RareItemSecond => Drop(2),
            JunctionGameplaySetting.RareItemThird => Drop(3),
            JunctionGameplaySetting.RareItemFourth => Drop(4),
            JunctionGameplaySetting.UnrestrictedScanRotation => ("Scan rotation", "Scan uses the source model's rotation restrictions.", "Scan allows unrestricted rotation."),
            _ => throw new NotSupportedException($"Unknown reviewed rule {change.Setting}.")
        };

        (string, string, string) Drop(int slot) => ($"Rare Item slot {slot}", $"Uses the standard Rare Item weight for slot {slot}.", $"Sets the Rare Item weight for slot {slot} to {value}.");
    }
}

public static partial class ModPreviewResources
{
}
