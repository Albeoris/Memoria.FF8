using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

/// <summary>Application composition root. Resource consumers do not reference the mod adapters.</summary>
internal sealed partial class GameResourceHost : IDisposable, IModelOverrideSource
{
    private readonly IResourceSource source;
    private readonly string materializationDirectory = Path.Combine(Path.GetTempPath(), "Memoria.FF8.Junction", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<ModFile, string> materializedFiles = [];
    private TonberryRecognitionCache? recognition;
    private readonly string gameDirectory;

    public GameResourceHost(string gameDirectory, string? applicationBaseDirectory = null, bool watch = true, string? modsDirectory = null)
    {
        this.gameDirectory = gameDirectory;
        source = GameDataSourceFactory.Open(gameDirectory);
        Resources = new ResourceService(source);
        Mods = new ModRuntime(applicationBaseDirectory, format => ModCompatibility.IsCompatible(source.Descriptor.Edition, format), Prepare, watch, additionalBaseDirectories: [gameDirectory], modsDirectory: modsDirectory);
        Resources.ReplacementFailed += (key, exception) =>
        {
            ModTextureLoadException? rejected = exception as ModTextureLoadException;
            ModDiagnostic diagnostic = new(exception is ResourceReplacementRejectedException ? "resource-rejected" : "replacement-failed", $"{key}: {exception.Message} Original pixels remain active.", rejected?.PackageId, rejected?.RelativePath);
            if (exception is ResourceReplacementRejectedException)
            {
                Mods.ReportResourceRejection(diagnostic);
            }
            else
            {
                Mods.ReportFailure(diagnostic);
            }
        };
        Pump(true);
    }

    public bool NativeGameplayModsSupported => ModCompatibility.IsCompatible(source.Descriptor.Edition, ModFormat.Memoria);
    public ResourceService Resources { get; }
    public ModRuntime Mods { get; }
    public GuardianForceAbilityMods GuardianForceMods { get; private set; } = GuardianForceAbilityMods.Empty;
    public Func<GuardianForceAbilityMods, ModCatalogSnapshot, ModPreparationResult>? PrepareGuardianForcePublication { get; set; }
    public FieldSceneMods SceneMods { get; private set; } = FieldSceneMods.Empty;
    public ModelMods ModelMods { get; private set; } = ModelMods.Empty;
    public StartupPolicy StartupPolicy { get; private set; } = StartupPolicy.Default;

    public FieldModel Apply(string modelId, FieldModel original) => ModelMods.Apply(modelId, original);
    public BattleModel Apply(string modelId, BattleModel original) => ModelMods.Apply(modelId, original);
    public BattleModel ApplyBattleBank(string modelId, string bank, BattleModel original) => ModelMods.ApplyBattleBank(modelId, bank, original);
    public IReadOnlyList<string> BattleBanks(string modelId) => ModelMods.BattleBanks(modelId);
    public bool HasModelOverride(string modelId) => ModelMods.HasModelOverride(modelId);
    public BattleStage Apply(string modelId, BattleStage original) => ModelMods.Apply(modelId, original);
    public WorldStaticModel Apply(string modelId, WorldStaticModel original) => ModelMods.Apply(modelId, original);

    public string ResolveGameFile(string dataRelativePath)
    {
        string relative = ModCatalogSnapshot.NormalizePath(dataRelativePath);
        ModFile? replacement = FindJunctionFile(relative);
        if (replacement is null)
        {
            if (source is IGameFileSource files)
            {
                return files.ResolveFile(relative);
            }
            throw new NotImplementedException("The selected resource provider does not implement file resolution.");
        }
        if (relative.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (replacement.ResolveFileSegment() is ModFileSegment segment)
                {
                    // FFmpeg's subfile protocol retains random access to the selected payload.
                    return FormattableString.Invariant($"subfile,,start,{segment.Offset},end,{checked(segment.Offset + segment.Length)},,:file:{segment.Path}");
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
            {
                Mods.ReportFailure(new("junction-replacement-failed", exception.Message, replacement.PackageId, replacement.RelativePath));
                throw;
            }
        }
        if (materializedFiles.TryGetValue(replacement, out string? cached))
        {
            return cached;
        }
        Directory.CreateDirectory(materializationDirectory);
        string path = Path.Combine(materializationDirectory, replacement.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + Path.GetExtension(relative));
        string temporary = path + ".tmp";
        try
        {
            using (FileStream destination = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                replacement.CopyTo(destination);
                destination.Flush(true);
            }
            File.Move(temporary, path);
            materializedFiles.Add(replacement, path);
            return path;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
        {
            File.Delete(temporary);
            Mods.ReportFailure(new("junction-replacement-failed", exception.Message, replacement.PackageId, replacement.RelativePath));
            throw;
        }
    }

    private ModFile? FindJunctionFile(string relative)
    {
        ModCatalogSnapshot catalog = Mods.Catalog;
        if (relative.StartsWith("movies/", StringComparison.OrdinalIgnoreCase) && relative.EndsWith(".cam", StringComparison.OrdinalIgnoreCase))
        {
            ModFile? video = FindJunctionFile(Path.ChangeExtension(relative, null) + "h.avi");
            if (video is not null)
            {
                // A lower-priority video's CAM may have a different frame cadence.
                // Video-only replacements retain the original native camera timeline.
                return catalog.FindAll(ModFormat.Junction, relative).FirstOrDefault(file => file.PackageId.Equals(video.PackageId, StringComparison.OrdinalIgnoreCase));
            }
        }
        if (!relative.StartsWith("movies/", StringComparison.OrdinalIgnoreCase) || !relative.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
        {
            return catalog.FindAll(ModFormat.Junction, relative).FirstOrDefault();
        }
        Dictionary<string, ModPackage> packages = catalog.Packages.Where(package => package.Format == ModFormat.Junction).ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<string, ModFile> files in catalog.Files.Where(file => file.Format == ModFormat.Junction).GroupBy(file => file.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            string extension = packages[files.Key].JunctionMetadata?.VideoExtension ?? "avi";
            string candidate = Path.ChangeExtension(relative, extension);
            ModFile? selected = files.FirstOrDefault(file => file.RelativePath.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected;
            }
        }
        return null;
    }

    public bool Pump(bool force = false)
    {
        Mods.PreparePending(force);
        return Mods.CommitPending();
    }

    private ModPreparationResult Prepare(ModCatalogSnapshot catalog)
    {
        ModPreparationResult? waiting = PrepareLocalization(catalog, out var localization);
        if (waiting is not null)
        {
            return waiting;
        }
        if (IsLocalizationOnlyChange(catalog))
        {
            return PrepareLocalizationPublication(localization!);
        }
        ModPackage[] behaviorPackages = catalog.Packages.Where(package => package.Status == ModEffectiveStatus.Active && package.JunctionMetadata?.Behaviors.Count > 0).ToArray();
        if (behaviorPackages.Length > 0 && (source.Descriptor.Edition != GameDataEdition.Classic || !source.Descriptor.Language.Equals("en", StringComparison.OrdinalIgnoreCase)))
        {
            return new ModPreparationResult(false, Diagnostics: behaviorPackages.Select(package => new ModDiagnostic("junction-behavior-profile-unsupported", "Reviewed executable behavior requires the Classic English resource profile.", package.Id)).ToArray());
        }
        StartupPolicy startupPolicy = StartupPolicy.FromCatalog(catalog);
        if (!JunctionEncodedResources.TryPrepare(catalog, out IReadOnlyList<EncodedResource> encodedResources, out IReadOnlyList<ModDiagnostic> encodedDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: encodedDiagnostics);
        }
        foreach (EncodedResource resource in encodedResources)
        {
            ModFile owner = catalog.Files.First(file => file.Format == ModFormat.Junction && $"{file.PackageId}:{file.RelativePath}" == resource.Content.Provenance);
            try
            {
                if (JunctionEncodedResources.FieldGroup(resource.Key) is not null)
                {
                    continue;
                }
                ResourceContent original = source.Find(resource.Key) ?? throw new NotSupportedException($"The selected game source does not provide resource {resource.Key}.");
                if (resource.Content.Format == JunctionEncodedResources.MainCharacterFormat)
                {
                    GameAssets.ValidateMainCharacterResource(resource.Content);
                }
                else
                {
                    BattleAssets.ValidateBattleModelResource(resource.Content, original);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or NotSupportedException or OverflowException or UnauthorizedAccessException)
            {
                return new ModPreparationResult(false, Diagnostics: [new("Junction.InvalidEncodedResource", exception.Message, owner.PackageId, owner.RelativePath)]);
            }
        }
        IReadOnlyList<ModDiagnostic> fieldResourceDiagnostics = ValidateFieldReplacementResources(catalog, encodedResources);
        if (fieldResourceDiagnostics.Any(diagnostic => diagnostic.Code == "Junction.InvalidFieldResource"))
        {
            return new ModPreparationResult(false, Diagnostics: encodedDiagnostics.Concat(fieldResourceDiagnostics).ToArray());
        }
        if (!JunctionTextResources.TryPrepare(catalog, out IReadOnlyList<StaticTextResource> textResources, out IReadOnlyList<ModDiagnostic> textDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: textDiagnostics);
        }
        IReadOnlyList<ModDiagnostic> musicDiagnostics = ExternalMusicResolver.Validate(catalog);
        if (musicDiagnostics.Count != 0)
        {
            return new ModPreparationResult(false, Diagnostics: musicDiagnostics);
        }
        if (!ModelMods.TryPrepare(catalog, out ModelMods modelMods, out IReadOnlyList<ModDiagnostic> modelDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: modelDiagnostics);
        }
        if (!FieldSceneMods.TryPrepare(catalog, out FieldSceneMods sceneMods, out IReadOnlyList<ModDiagnostic> sceneDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: sceneDiagnostics);
        }
        IReadOnlyList<ModDiagnostic> sceneResourceDiagnostics = ValidateSceneModResources(sceneMods, encodedResources);
        if (sceneResourceDiagnostics.Count != 0)
        {
            return new ModPreparationResult(false, Diagnostics: sceneResourceDiagnostics);
        }
        if (!GuardianForceAbilityMods.TryPrepare(catalog, out GuardianForceAbilityMods guardianMods, out IReadOnlyList<ModDiagnostic> guardianDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: guardianDiagnostics);
        }
        ModPreparationResult guardianPublication = PrepareGuardianForcePublication?.Invoke(guardianMods, catalog) ?? ModPreparationResult.Accepted;
        if (!guardianPublication.Success)
        {
            return guardianPublication;
        }
        List<ITextureReplacementResolver> resolvers = [];
        List<string> usable = [];
        usable.AddRange(localization!.Layers.Select(layer => layer.PackageId));
        usable.AddRange(guardianMods.Layers.Select(layer => layer.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(modelMods.Variants.Select(variant => variant.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(sceneMods.Patches.Select(patch => patch.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(catalog.Files.Where(file => file.Format == ModFormat.Junction && !(JunctionEncodedResources.Address(file.RelativePath) is ResourceKey key && JunctionEncodedResources.FieldGroup(key) is not null) && !JunctionEncodedResources.IsFieldMetadata(file.RelativePath) && !JunctionEncodedResources.IsAncillaryFile(file.RelativePath)).Select(file => file.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(encodedResources.Select(resource => catalog.Files.First(file => $"{file.PackageId}:{file.RelativePath}" == resource.Content.Provenance).PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(behaviorPackages.Select(package => package.Id));
        DemasterInspection demaster = DemasterTextureResolver.Inspect(source.Descriptor, catalog);
        if (catalog.Files.Any(file => file.Format == ModFormat.Demaster))
        {
            resolvers.Add(new DemasterTextureResolver(catalog));
            usable.AddRange(demaster.UsablePackageIds);
        }
        using ResourceSourceView candidateSource = new(source, encodedResources);
        DemasterInspection ffnx = FfnxTextureResolver.Inspect(source.Descriptor, catalog, candidateSource, source as IGameNativeMemorySource);
        if (catalog.Files.Any(file => file.Format == ModFormat.Junction && file.RelativePath.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase)))
        {
            resolvers.Add(new FfnxTextureResolver(catalog));
            usable.AddRange(ffnx.UsablePackageIds);
        }
        TonberryTextureResolver? tonberry = TonberryTextureResolver.Create(source.Descriptor, catalog, out IReadOnlyList<ModDiagnostic> tonberryDiagnostics, recognition);
        IReadOnlyList<ModDiagnostic> diagnostics = encodedDiagnostics.Concat(fieldResourceDiagnostics).Concat(tonberryDiagnostics).Concat(demaster.Diagnostics).Concat(ffnx.Diagnostics).Concat(guardianPublication.Diagnostics ?? []).ToArray();
        if (diagnostics.Any(diagnostic => diagnostic.Code == "Tonberry.MalformedRow" && Mods.Catalog.Files.Any(old => old.PackageId == diagnostic.PackageId && old.RelativePath == diagnostic.RelativePath)))
        {
            return new ModPreparationResult(false, Diagnostics: diagnostics);
        }
        if (tonberry is not null)
        {
            recognition = tonberry.RecognitionCache;
            resolvers.Add(tonberry);
            usable.AddRange(tonberry.UsablePackageIds);
        }
        else
        {
            recognition = null;
        }
        ITextureReplacementResolver? resolver = resolvers.Count switch
        {
            0 => null,
            1 => resolvers[0],
            _ => new OrderedTextureResolver(resolvers.ToArray())
        };
        ResourceService.PreparedResources prepared;
        try
        {
            prepared = Resources.Prepare(resolver, textResources, encodedResources, localization!.Catalog);
        }
        catch (ResourceReloadRequiredException exception)
        {
            return new ModPreparationResult(false, Diagnostics: [new("resource-reload-required", exception.Message)]);
        }
        return new ModPreparationResult(true, usable, diagnostics, () =>
        {
            System.Diagnostics.Stopwatch publicationClock = System.Diagnostics.Stopwatch.StartNew();
            localization!.InputRevision?.ValidatePublication();
            prepared.Commit(notify: false, beforePublish: guardianPublication.Commit);
            CommittedLocalizations?.InputRevision?.Dispose();
            CommittedLocalizations = localization;
            GuardianForceMods = guardianMods;
            SceneMods = sceneMods;
            ModelMods = modelMods;
            StartupPolicy = startupPolicy;
            LastLocalizationChangedKeys = localization.ChangedKeys;
            LastLocalizationPublicationMilliseconds = publicationClock.Elapsed.TotalMilliseconds;
        }, prepared.Notify);
    }

    public void Dispose()
    {
        localizationCancellation.Cancel();
        CommittedLocalizations?.InputRevision?.Dispose();
        if (localizationTask is { } pendingLocalization)
        {
            _ = pendingLocalization.ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    task.Result.InputRevision?.Dispose();
                }
                else
                {
                    _ = task.Exception;
                }
            }, TaskScheduler.Default);
        }
        Mods.Dispose();
        recognition?.Clear();
        source.Dispose();
        if (Directory.Exists(materializationDirectory))
        {
            try
            {
                Directory.Delete(materializationDirectory, recursive: true);
            }
            catch (IOException)
            {
                // A decoder can retain a cache file until its process has exited.
            }
            catch (UnauthorizedAccessException)
            {
                // The operating system can retain an open decoder's cache file.
            }
        }
    }
}
