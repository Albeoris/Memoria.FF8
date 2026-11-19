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

public sealed record ModPreviewEntry(string Id, string Category, string Group, string Name);
public sealed record ModPreviewSide(string Text, PixelImage? Image = null)
{
    public EditableModel? Model { get; init; }
    public bool CanUseItem { get; init; }
    public byte[]? AudioPcm { get; init; }
}

public sealed record ModPreviewComparison(ModPreviewSide Before, ModPreviewSide After);
public sealed record ModPreviewResourcesProfile(string GameDirectory, string? ModelSourceDirectory = null, string? Language = null)
{
    public Func<FieldAssets, PixelImage>? RenderField { get; init; }
    public Func<FieldAssets, IReadOnlyList<FieldBackgroundPatch>, PixelImage>? RenderFieldPatch { get; init; }
    public Func<ModPreviewMediaInput, bool, double, CancellationToken, ModPreviewSide>? DecodeMedia { get; init; }
    public double MediaSeconds { get; init; }
}

public sealed record ModPreviewMediaInput(string Name, string? Path, Func<byte[]>? Read = null);

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

    private ModPreviewDocument(ModPackage package, List<ModFile> files, JunctionManifestMetadata? junction)
    {
        Name = package.Name;
        catalog = new(0, [package], files);
        this.files = files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
        this.junction = junction;
        localization = new(() => LocalizationProjectIO.ReadForAuthoring(package.RootDirectory));
        List<ModPreviewEntry> entries = files.Where(file => !file.RelativePath.EndsWith(TextureModMapping.Extension, StringComparison.OrdinalIgnoreCase)).Select(GetEntry).ToList();
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
    }

    public string Name { get; }
    public IReadOnlyList<ModPreviewEntry> Entries { get; }
    public ModCatalogSnapshot PackageCatalog => catalog;
    public const string WorldSceneId = "scene:world";

    internal static ModPreviewDocument Open(ModPackage package, CancellationToken cancellationToken, bool useInstalledOptions = true)
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
                FileInfo info = new(path);
                long length = info.Length;
                long stamp = info.LastWriteTimeUtc.Ticks;
                byte[] Read()
                {
                    using FileStream stream = File.OpenRead(path);
                    if (stream.Length != length || File.GetLastWriteTimeUtc(path).Ticks != stamp)
                    {
                        throw new IOException("The mod changed on disk. Reopen its preview to compare the current version.");
                    }
                    return ReadSample(stream, length);
                }
                files.Add(new(package.Id, package.Format, relative, files.Count, Read, cacheContent: false) { AbsolutePath = path, ContentStamp = (length, stamp) });
            }
        }
        else
        {
            throw new NotSupportedException("This package format does not support comparison.");
        }
        return new(package, files, junction);
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
        if (!files.TryGetValue(entry.Id, out ModFile? file))
        {
            throw new ArgumentException("The selected change does not belong to this package.", nameof(entry));
        }
        if (file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("fields/", StringComparison.OrdinalIgnoreCase) && profile.RenderFieldPatch is { } renderPatch)
        {
            return CompareFieldPatch(file, profile, renderPatch);
        }
        if (file.Format == ModFormat.Memoria && file.RelativePath.Equals(GuardianForceAbilityMods.PayloadPath, StringComparison.OrdinalIgnoreCase))
        {
            return CompareGuardianAbilities(file, profile);
        }
        if (file.Format == ModFormat.Memoria && EncounterMods.IsPayloadPath(file.RelativePath))
        {
            return CompareEncounter(file);
        }
        if (StatusMods.IsStatusDocument(file))
        {
            return CompareStatus(file);
        }
        if (entry.Category == "Text" && entry.Group == "Localization" && !TextureModPaths.IsImage(file.RelativePath))
        {
            return CompareLocalization(file, cancellationToken);
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
        if (entry.Category is "Audio" or "Video" && profile.DecodeMedia is { } decode)
        {
            return CompareMedia(file, profile, entry.Category == "Video", decode, cancellationToken);
        }
        if (TextureModPaths.IsImage(file.RelativePath))
        {
            if (ModPreviewResources.CompareTexture(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language, catalog, file.RelativePath, cancellationToken, profile.RenderField) is { } texture)
            {
                return texture;
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

    private ModPreviewComparison CompareFieldPatch(ModFile file, ModPreviewResourcesProfile profile, Func<FieldAssets, IReadOnlyList<FieldBackgroundPatch>, PixelImage> render)
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

    private static ModPreviewComparison CompareGuardianAbilities(ModFile file, ModPreviewResourcesProfile profile)
    {
        using IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        using GameAssets assets = new(profile.GameDirectory, resources: new(source, profile.Language));
        NativeCharacterVitals stock = assets.ReadNewGameCharacterVitals();
        GuardianForceAbilityPatchDocument patch = GuardianForceAbilityMods.Parse(file.ReadBytes());
        var before = stock.ResolveGuardianForceTemplates();
        GuardianForceAbilityComposition after = GuardianForceAbilityComposer.Compose(before, [new(file.PackageId, patch)]);
        int[] modified = patch.Operations.Select(operation => operation.GuardianForce).Distinct().Order().ToArray();
        string Read(IEnumerable<GuardianForceAbilityTemplate> list) => string.Join(", ", list.Select(ability => ability.InitialLearned ? $"{ability.Ability} (learned)" : $"{ability.Ability} ({ability.RequiredPoints} AP)"));
        string oldText = string.Join("\n\n", modified.Select(id => $"GF {id}\n{Read(before[id])}"));
        string newText = string.Join("\n\n", modified.Select(id => $"GF {id}\n{Read(after.Templates[id])}"));
        return new(new(oldText), new(newText));
    }

    private ModPreviewComparison CompareEncounter(ModFile file)
    {
        if (!EncounterMods.TryPrepare(catalog, out EncounterMods encounters, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
        }
        EncounterPatchDocument patch = encounters.Layers.Single(layer => layer.PayloadPath == file.RelativePath).Document;
        string title = patch.Title ?? patch.Encounter;
        string changes = string.Join("\n", patch.Operations.Select(operation =>
            string.Join(" · ", new[] { operation.Kind.ToString(), operation.Actor ?? operation.ActorTemplate ?? operation.Group, operation.Property, operation.Value.ValueKind == JsonValueKind.Undefined ? null : operation.Value.ToString() }.Where(value => !string.IsNullOrEmpty(value)))));
        return new(new($"{title}\nOriginal encounter rules."), new($"{title}\n{changes}"));
    }

    private ModPreviewComparison CompareMedia(ModFile file, ModPreviewResourcesProfile profile, bool video, Func<ModPreviewMediaInput, bool, double, CancellationToken, ModPreviewSide> decode, CancellationToken token)
    {
        string name = Path.GetFileName(file.RelativePath);
        string stem = Path.GetFileNameWithoutExtension(name);
        if (!video && Path.GetExtension(name).ToLowerInvariant() is not (".ogg" or ".wav" or ".flac" or ".mp3" or ".opus"))
        {
            return new(new("Audio sample unavailable."), new("This audio format has no decoder for the comparison."));
        }
        using IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        ResourceService resources = new(source, profile.Language);
        IGameFileSource? original = resources.GetSource<IGameFileSource>();
        string? originalPath = null;
        if (original is not null && video)
        {
            originalPath = new[] { $"movies/{stem}.avi", $"movies/{stem}h.avi", $"movies/{name}", file.RelativePath }.Select(original.ResolveFile).FirstOrDefault(File.Exists);
        }
        if (original is not null && !video)
        {
            var names = new List<string> { file.RelativePath, $"music/stream/{name}", $"music/stream/{stem}.wav" };
            if (junction is not null)
            {
                ExternalMusicResolver resolver = new(catalog);
                try
                {
                    NativeMusicCatalog music = new(resources);
                    for (int track = 0; track < 117; track++)
                    {
                        string native = music.MusicFileResolved(track).Replace('\\', '/');
                        if (resolver.Resolve(native)?.File == file && native.StartsWith("Data/", StringComparison.OrdinalIgnoreCase))
                        {
                            names.Insert(0, native[5..]);
                        }
                    }
                }
                catch (NotSupportedException)
                {
                    // Some installations expose loose tracks without a supported executable music table.
                }
            }
            originalPath = names.Select(original.ResolveFile).FirstOrDefault(File.Exists);
        }
        ModPreviewSide before = originalPath is null ? new("Original media could not be matched.") : Decode(new(Path.GetFileName(originalPath), originalPath));
        ModPreviewSide after = Decode(new(name, file.AbsolutePath, file.ReadBytes));
        return new(before, after);

        ModPreviewSide Decode(ModPreviewMediaInput input)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                return decode(input, video, profile.MediaSeconds, token);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or NotSupportedException or TimeoutException or System.ComponentModel.Win32Exception)
            {
                return new($"{input.Name}\n{error.Message}");
            }
        }
    }

    private ModPreviewComparison CompareStatus(ModFile file)
    {
        if (!StatusMods.TryPrepare(catalog, out StatusMods status, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
        }
        StatusStatModifier[] changed = status.Modifiers.Where(rule => status.ModifierSource(rule.Status, rule.Stat) == file).ToArray();
        string Before(StatusStatModifier rule)
        {
            int reduction = StatusStatRules.Native.FirstOrDefault(native => native.Status == rule.Status && native.Stat == rule.Stat).Value;
            return $"{rule.Status} · {rule.Stat} −{reduction}%";
        }
        return new(new(string.Join("\n", changed.Select(Before))), new(string.Join("\n", changed.Select(rule => $"{rule.Status} · {rule.Stat} −{rule.Value}%"))));
    }

    private ModPreviewComparison CompareLocalization(ModFile file, CancellationToken cancellationToken)
    {
        LocalizationProject project = localization.Value;
        string name = Path.GetFileNameWithoutExtension(file.RelativePath);
        var changed = project.Entries.Where(row => !string.IsNullOrEmpty(row.Target) && row.Target != row.Source);
        var matching = changed.Where(row => Path.GetFileNameWithoutExtension(row.File ?? "").Equals(name, StringComparison.OrdinalIgnoreCase)).Take(10).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        if (matching.Length == 0)
        {
            matching = changed.OrderBy(row => row.Category == "Fields" ? 0 : 1).ThenBy(row => row.File, StringComparer.OrdinalIgnoreCase).Take(10).ToArray();
        }
        string Format(bool original) => matching.Length == 0 ? "No translated text is available." : string.Join("\n\n", matching.Select(row => $"{row.File}\n{(original ? row.Source : row.Target) ?? "—"}"));
        return new(new(Format(true)), new(Format(false)));
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
        PixelImage before = render(original.LoadField(id));
        cancellationToken.ThrowIfCancellationRequested();
        using IResourceSource modifiedSource = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
        ResourceService modifiedResources = new(modifiedSource, profile.Language);
        modifiedResources.Prepare(new OrderedTextureResolver(textures, new FfnxTextureResolver(subset)), encodedResources: replacements).Commit();
        using GameAssets modified = new(profile.GameDirectory, resources: modifiedResources);
        FieldAssets applied = modified.LoadField(id);
        FieldBackgroundPatch[] patches = scenes.Patches.Where(owned => owned.Patch.FieldId == id).SelectMany(owned => owned.Patch.Backgrounds).ToArray();
        return new(new(field, before), new(field, patches.Length != 0 && profile.RenderFieldPatch is { } renderPatches ? renderPatches(applied, patches) : render(applied)));
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
        if (JunctionEncodedResources.Address(file.RelativePath) is not { } key || !key.Path.StartsWith("battle/", StringComparison.Ordinal))
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
        ModelCatalogEntry? entry = beforeAssets.EnumerateModelCatalog(cancellationToken).FirstOrDefault(model => model.ResourcePath.Equals(key.Path, StringComparison.OrdinalIgnoreCase));
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
        int[] changed = Enumerable.Range(0, Math.Max(before.Count, after.Count)).Where(index => !string.Equals(before.ElementAtOrDefault(index), after.ElementAtOrDefault(index), StringComparison.Ordinal)).Take(12).ToArray();
        string Sample(IReadOnlyList<string> lines) => changed.Length == 0 ? "No text changes." : string.Join("\n\n", changed.Select(index => $"Message {index}\n{lines.ElementAtOrDefault(index) ?? "—"}"));
        return new(new(Sample(before)), new(Sample(after)));
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

    private static ModPreviewEntry GetEntry(ModFile file)
    {
        string path = file.RelativePath;
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
            ("music", _, _) => ("Audio", "Music"),
            ("voice", _, _) => ("Audio", "Voices"),
            (_, _, ".ogg" or ".wav" or ".flac" or ".mp3" or ".opus" or ".sgt") => ("Audio", "Tracks"),
            (_, _, ".avi" or ".mp4" or ".webm" or ".mkv" or ".bik") => ("Video", "Movies"),
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
