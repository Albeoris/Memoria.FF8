using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

/// <summary>Application composition root. Resource consumers do not reference the mod adapters.</summary>
internal sealed partial class GameResourceHost : IDisposable, IModelOverrideSource
{
    private readonly IResourceSource source;
    private readonly string? resourceLanguage;
    private readonly string materializationDirectory = Path.Combine(Path.GetTempPath(), "Memoria.FF8.Junction", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<ModFile, string> materializedFiles = [];
    private TonberryRecognitionCache? recognition;
    private readonly string gameDirectory;
    private readonly string? modelSourceDirectory;
    private NativeTextureMods nativeTextureMods = NativeTextureMods.Empty;
    private sealed record ModelPublication(ModelMods Models, FieldSpringBoneMods Springs);
    private ModelPublication modelPublication = new(ModelMods.Empty, FieldSpringBoneMods.Empty);

    public GameResourceHost(string gameDirectory, string? applicationBaseDirectory = null, bool watch = true, string? modsDirectory = null, string? resourceLanguage = null, string? modelSourceDirectory = null, ModDirectories? modLocations = null, bool connectEncounterMods = false) : this(gameDirectory, CancellationToken.None, applicationBaseDirectory, watch, modsDirectory, resourceLanguage, modelSourceDirectory, modLocations, connectEncounterMods)
    {
    }

    private GameResourceHost(string gameDirectory, CancellationToken startupCancellationToken, string? applicationBaseDirectory = null, bool watch = true, string? modsDirectory = null, string? resourceLanguage = null, string? modelSourceDirectory = null, ModDirectories? modLocations = null, bool connectEncounterMods = false)
    {
        if (connectEncounterMods)
        {
            PrepareEncounterPublication = PrepareEncounterModPublication;
        }
        this.gameDirectory = gameDirectory;
        this.modelSourceDirectory = modelSourceDirectory;
        this.resourceLanguage = Memoria.FF8.Platform.GameResourceLanguages.Normalize(resourceLanguage);
        source = GameDataSourceFactory.Open(gameDirectory, modelSourceDirectory, this.resourceLanguage);
        Resources = new ResourceService(source, this.resourceLanguage);
        Mods = new ModRuntime(applicationBaseDirectory ?? ApplicationInstallation.DataDirectory, format => ModCompatibility.IsCompatible(source.Descriptor.Edition, format), Prepare, watch, additionalBaseDirectories: [gameDirectory], modsDirectory: modsDirectory, junctionPackageAllowed: CanReadJunctionPackage, nativeRootUnavailable: NativeRootUnavailable, edition: source.Descriptor.Edition.ToString(), language: this.resourceLanguage ?? source.Descriptor.Language, obsoleteBundledPackage: path => Path.GetFileName(path) == OfficialModCatalog.LegacyGirlNextDoorSettingsKey && GirlNextDoorMigration.IsComplete(Mods!.LegacyModsDirectory), modLocations: modLocations);
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
        try
        {
            RecoverModLibrary(applicationBaseDirectory ?? Memoria.FF8.Platform.ApplicationInstallation.DataDirectory);
            CompleteStartupPreparation(startupCancellationToken);
            ValidateModLibraryStartup(startupCancellationToken);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public bool NativeGameplayModsSupported => ModCompatibility.IsCompatible(source.Descriptor.Edition, ModFormat.Memoria);
    public ResourceService Resources { get; }
    public Func<ResourceService.PreparedResources, TexturePublication>? PrepareTexturePublication { get; set; }
    public ItemOverrideMods ItemOverrides { get; private set; } = ItemOverrideMods.Empty;
    public ModRuntime Mods { get; }
    public GuardianForceAbilityMods GuardianForceMods { get; private set; } = GuardianForceAbilityMods.Empty;
    public GuardianForceRules GuardianRules { get; private set; } = GuardianForceRules.Empty;
    public Func<GuardianForceAbilityMods, ModCatalogSnapshot, ModPreparationResult>? PrepareGuardianForcePublication { get; set; }
    public EncounterMods EncounterMods { get; private set; } = EncounterMods.Empty;
    public Func<EncounterMods, ModCatalogSnapshot, IReadOnlyList<EncodedResource>, ModPreparationResult>? PrepareEncounterPublication { get; set; }
    public FieldSceneMods SceneMods { get; private set; } = FieldSceneMods.Empty;
    public ModelMods ModelMods => modelPublication.Models;
    public StartupPolicy StartupPolicy { get; private set; } = StartupPolicy.Default;
    public GameplayTuningPolicy GameplayPolicy { get; private set; } = GameplayTuningPolicy.Empty;

    public FieldModel Apply(string modelId, FieldModel original)
    {
        ModelPublication publication = modelPublication;
        return publication.Springs.Apply(modelId, original, publication.Models.Apply(modelId, original));
    }
    public BattleModel Apply(string modelId, BattleModel original) => ModelMods.Apply(modelId, original);
    public BattleModel ApplyBattleBank(string modelId, string bank, BattleModel original) => ModelMods.ApplyBattleBank(modelId, bank, original);
    public IReadOnlyList<string> BattleBanks(string modelId) => ModelMods.BattleBanks(modelId);
    public bool HasModelOverride(string modelId) => ModelMods.HasModelOverride(modelId);
    public BattleStage Apply(string modelId, BattleStage original) => ModelMods.Apply(modelId, original);
    public WorldStaticModel Apply(string modelId, WorldStaticModel original) => ModelMods.Apply(modelId, original);

    public string ResolveGameFile(string dataRelativePath)
    {
        string relative = ModCatalogSnapshot.NormalizePath(dataRelativePath);
        ModFile? replacement = FindFileReplacement(Mods.Catalog, relative, movieSubtitles.Components);
        if (replacement is null)
        {
            if (Resources.GetSource<IGameFileSource>() is { } files)
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
                ReportFileReplacementFailure(replacement, exception);
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
            ReportFileReplacementFailure(replacement, exception);
            throw;
        }
    }

    private void ReportFileReplacementFailure(ModFile replacement, Exception exception)
    {
        string code = replacement.Format == ModFormat.Memoria ? "Memoria.MovieReplacementFailed" : "junction-replacement-failed";
        Mods.ReportFailure(new(code, exception.Message, replacement.PackageId, replacement.RelativePath));
    }

    public bool Pump(bool force = false)
    {
        Mods.PreparePending(force);
        return Mods.CommitPending();
    }

    private ModPreparationResult Prepare(ModCatalogSnapshot catalog)
    {
        CandidatePreparation candidate = GetCandidatePreparation(catalog);
        try
        {
            ModPreparationResult result = PrepareCore(catalog, candidate);
            if (!result.Deferred && (!result.Success || !candidate.PublicationPrepared))
            {
                DiscardCandidate(candidate);
            }
            if (result.Deferred)
            {
                return result with
                {
                    Discard = () =>
                    {
                        result.Discard?.Invoke();
                        DiscardCandidate(candidate);
                    }
                };
            }
            return result;
        }
        catch
        {
            DiscardCandidate(candidate);
            throw;
        }
    }

    private ModPreparationResult PrepareCore(ModCatalogSnapshot catalog, CandidatePreparation candidate)
    {
        if (candidate.Resources is not null)
        {
            return AdvanceCandidate(candidate);
        }
        if (!candidate.Native.IsCompleted)
        {
            return DeferredCandidate(candidate);
        }
        if (!candidate.Native.IsCompletedSuccessfully)
        {
            Exception? error = candidate.Native.Exception?.GetBaseException();
            DiscardCandidate(candidate);
            throw new InvalidDataException("Native texture preparation did not complete.", error);
        }
        NativePreparation nativePreparation = candidate.Native.Result;
        if (!nativePreparation.Success)
        {
            DiscardCandidate(candidate);
            return ModPreparationResult.RejectPackagePayloads(nativePreparation.Diagnostics.Where(diagnostic => diagnostic.Code == "Memoria.InvalidTexture").ToArray());
        }
        ModPreparationResult? waiting = PrepareLocalization(catalog, out var localization);
        if (waiting is not null)
        {
            return waiting;
        }
        if (!StatusMods.TryPrepare(catalog, out StatusMods statusMods, out IReadOnlyList<ModDiagnostic> statusDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: statusDiagnostics);
        }
        if (IsLocalizationOnlyChange(catalog) && GameplayPolicy.StatusStatModifiers.SequenceEqual(statusMods.Modifiers))
        {
            return PrepareLocalizationPublication(localization!);
        }
        if (!TryPrepareMovieSubtitles(catalog, out MovieSubtitlePublication subtitles, out IReadOnlyList<ModDiagnostic> subtitleDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: subtitleDiagnostics);
        }
        ModPackage[] behaviorPackages = catalog.Packages.Where(package => package.Status == ModEffectiveStatus.Active && (package.JunctionMetadata?.Behaviors.Count > 0 || package.JunctionMetadata?.GameplayOverrides.Count > 0)).ToArray();
        if (behaviorPackages.Length > 0 && (source.Descriptor.Edition != GameDataEdition.Classic || !source.Descriptor.Language.Equals("en", StringComparison.OrdinalIgnoreCase)))
        {
            return new ModPreparationResult(false, Diagnostics: behaviorPackages.Select(package => new ModDiagnostic("junction-behavior-profile-unsupported", "Reviewed executable behavior requires the Classic English resource profile.", package.Id)).ToArray());
        }
        StartupPolicy startupPolicy = StartupPolicy.FromCatalog(catalog);
        GameplayTuningPolicy gameplayPolicy = GameplayTuningPolicy.FromCatalog(catalog, statusMods);
        if (!JunctionEncodedResources.TryPrepare(catalog, out IReadOnlyList<EncodedResource> encodedResources, out IReadOnlyList<ModDiagnostic> encodedDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: encodedDiagnostics);
        }
        List<ModDiagnostic> staticFileDiagnostics = [];
        ResourceService dataCandidate = new(source, resourceLanguage);
        dataCandidate.Prepare(null, encodedResources: encodedResources).Commit();
        using NativeDataResourceValidator dataValidator = new(dataCandidate);
        foreach (EncodedResource resource in encodedResources.OrderBy(resource => NativeDataResourceFiles.Classify(resource.Key) == NativeDataResourceKind.Kernel ? 0 : NativeDataResourceFiles.Classify(resource.Key) == NativeDataResourceKind.Enemy ? 2 : 1))
        {
            ModFile owner = catalog.Files.First(file => file.Format == ModFormat.Junction && $"{file.PackageId}:{file.RelativePath}" == resource.Content.Provenance);
            try
            {
                if (JunctionEncodedResources.FieldGroup(resource.Key) is not null)
                {
                    continue;
                }
                ResourceContent? original = source.Find(resource.Key);
                if (NativeDataResourceFiles.Classify(resource.Key) is NativeDataResourceKind kind)
                {
                    if (original is null && kind is not (NativeDataResourceKind.Texture or NativeDataResourceKind.CompressedImage or NativeDataResourceKind.WorldSettings))
                    {
                        throw new NotSupportedException($"The selected game source does not provide resource {resource.Key}.");
                    }
                    dataValidator.Validate(resource.Content, original);
                    if (kind == NativeDataResourceKind.WorldSettings && resource.Key != NativeResourceNames.WorldSettings(Resources.ResourceLanguage) && !encodedResources.Any(member => member.Key == NativeResourceNames.WorldSettings(Resources.ResourceLanguage) && member.Content.Provenance.StartsWith($"{owner.PackageId}:", StringComparison.Ordinal)))
                    {
                        staticFileDiagnostics.Add(new("resource-language-variant-missing", $"This package provides {resource.Key}, but no variant {NativeResourceNames.WorldSettings(Resources.ResourceLanguage)} for the selected resource language. The package remains active; resources keep their original identities and other language variants are used only when requested.", owner.PackageId, owner.RelativePath));
                    }
                    else if (kind is NativeDataResourceKind.CompressedImage or NativeDataResourceKind.Texture && !NativeDataResourceFiles.IsDisplayedImage(resource.Key))
                    {
                        staticFileDiagnostics.Add(new("resource-unused", $"Resource {resource.Key} was validated and published, but the current interface does not display this native image.", owner.PackageId, owner.RelativePath));
                    }
                    else if (NativeDataResourceFiles.IsStandaloneRefinementTable(resource.Key))
                    {
                        staticFileDiagnostics.Add(new("resource-unused", $"Standalone refinement table {resource.Key} was validated and published, but the current native menu consumes the embedded menu-group tables.", owner.PackageId, owner.RelativePath));
                    }
                    else if (original is null && kind != NativeDataResourceKind.WorldSettings)
                    {
                        staticFileDiagnostics.Add(new("resource-unused", $"Resource {resource.Key} was validated and published, but the selected source provides no original target for this identity.", owner.PackageId, owner.RelativePath));
                    }
                }
                else if (JunctionEncodedResources.IsStaticFileResource(resource.Key))
                {
                    original = original ?? throw new NotSupportedException($"The selected game source does not provide resource {resource.Key}.");
                    if (!original.Format.Equals(resource.Content.Format, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException($"The selected game source does not provide the registered file format for {resource.Key}.");
                    }
                    staticFileDiagnostics.Add(new("resource-unused", $"Resource {resource.Key} was published, but the current renderer has no consumer for this file format.", owner.PackageId, owner.RelativePath));
                }
                else if (resource.Content.Format == JunctionEncodedResources.MainCharacterFormat)
                {
                    GameAssets.ValidateMainCharacterResource(resource.Content, original);
                }
                else
                {
                    original = original ?? throw new NotSupportedException($"The selected game source does not provide resource {resource.Key}.");
                    BattleAssets.ValidateBattleModelResource(resource.Content, original);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or NotSupportedException or OverflowException or UnauthorizedAccessException)
            {
                string? group = NativeDataResourceFiles.Group(resource.Key);
                ModFile[] relatedOwners = group is null ? [owner] : encodedResources.Where(member => NativeDataResourceFiles.Group(member.Key) == group).Select(member => catalog.Files.First(file => $"{file.PackageId}:{file.RelativePath}" == member.Content.Provenance)).ToArray();
                return new ModPreparationResult(false, Diagnostics: relatedOwners.Select(related => new ModDiagnostic("Junction.InvalidEncodedResource", exception.Message, related.PackageId, related.RelativePath)).ToArray());
            }
        }
        IReadOnlyList<ModDiagnostic> fieldResourceDiagnostics = ValidateFieldReplacementResources(catalog, encodedResources, out IReadOnlyList<EncodedResource> acceptedFieldResources);
        encodedResources = acceptedFieldResources;
        if (!JunctionTextResources.TryPrepare(catalog, out IReadOnlyList<StaticTextResource> textResources, out IReadOnlyList<ModDiagnostic> textDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: textDiagnostics);
        }
        foreach (StaticTextResource text in textResources.Where(text => text.Key != StaticTextResource.CardNames && text.Key != StaticTextResource.BattleScans && text.Key != StaticTextResource.DrawPointMessages))
        {
            staticFileDiagnostics.Add(new("resource-unused", $"Text table {text.Key} was published, but the current interface has no native indexed consumer for it. Authored interface localization remains separate.", text.PackageId, text.RelativePath));
        }
        if (!PrepareVoices(catalog, encodedResources, out ExternalVoiceResolver? voiceResolver, out IReadOnlyList<EncodedResource> voiceResources, out IReadOnlyList<ModDiagnostic> voiceDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: encodedDiagnostics.Concat(fieldResourceDiagnostics).Concat(voiceDiagnostics).ToArray());
        }
        encodedResources = encodedResources.Concat(voiceResources).ToArray();
        IReadOnlyList<ModDiagnostic> musicDiagnostics = ExternalMusicResolver.Validate(catalog);
        if (musicDiagnostics.Count != 0)
        {
            return new ModPreparationResult(false, Diagnostics: musicDiagnostics);
        }
        if (!ModelMods.TryPrepare(catalog, out ModelMods modelMods, out IReadOnlyList<ModDiagnostic> modelDiagnostics))
        {
            return ModPreparationResult.RejectPackagePayloads(modelDiagnostics.Where(diagnostic => diagnostic.Code == "Memoria.InvalidModel").ToArray());
        }
        if (!FieldSpringBoneMods.TryPrepare(catalog, out FieldSpringBoneMods springMods, out IReadOnlyList<ModDiagnostic> springDiagnostics, modelMods.Variants))
        {
            return ModPreparationResult.RejectPackagePayloads(springDiagnostics);
        }
        springDiagnostics = ValidateSpringBoneResources(springMods, modelMods, encodedResources);
        if (springDiagnostics.Count != 0)
        {
            return new ModPreparationResult(false, Diagnostics: springDiagnostics);
        }
        if (!FieldSceneMods.TryPrepare(catalog, out FieldSceneMods sceneMods, out IReadOnlyList<ModDiagnostic> sceneDiagnostics))
        {
            return ModPreparationResult.RejectPackagePayloads(sceneDiagnostics);
        }
        IReadOnlyList<ModDiagnostic> sceneResourceDiagnostics = ValidateSceneModResources(sceneMods, encodedResources);
        if (sceneResourceDiagnostics.Count != 0)
        {
            return new ModPreparationResult(false, Diagnostics: sceneResourceDiagnostics);
        }
        if (!GuardianForceAbilityMods.TryPrepare(catalog, out GuardianForceAbilityMods guardianMods, out IReadOnlyList<ModDiagnostic> guardianDiagnostics))
        {
            return ModPreparationResult.RejectPackagePayloads(guardianDiagnostics);
        }
        if (!GuardianForceRules.TryPrepare(catalog, out GuardianForceRules guardianRules, out IReadOnlyList<ModDiagnostic> guardianRuleDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: guardianRuleDiagnostics);
        }
        ModPreparationResult guardianPublication = PrepareGuardianForcePublication?.Invoke(guardianMods, catalog) ?? ModPreparationResult.Accepted;
        if (!guardianPublication.Success)
        {
            return guardianPublication;
        }
        if (!EncounterMods.TryPrepare(catalog, out EncounterMods encounterMods, out IReadOnlyList<ModDiagnostic> encounterDiagnostics))
        {
            return ModPreparationResult.RejectPackagePayloads(encounterDiagnostics);
        }
        ModPreparationResult encounterPublication = PrepareEncounterPublication?.Invoke(encounterMods, catalog, encodedResources) ?? (encounterMods.Layers.IsEmpty ? ModPreparationResult.Accepted : new ModPreparationResult(false, Diagnostics: encounterMods.Layers.Select(layer => new ModDiagnostic("Memoria.UnsupportedEncounterRuntime", "Encounter documents require a supported encounter binding and runtime publication provider; no encounter changes were published.", layer.PackageId, layer.PayloadPath)).ToArray()));
        if (!encounterPublication.Success)
        {
            return encounterPublication;
        }
        List<ITextureReplacementResolver> resolvers = [];
        List<string> usable = [];
        usable.AddRange(subtitles.PackageIds);
        usable.AddRange(catalog.Files.Where(file => file.Format == ModFormat.Memoria && (MoviePackagePaths.IsNativeMovieVideo(file.RelativePath) || MoviePackagePaths.IsNativeMovieCamera(file.RelativePath))).Select(file => file.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        NativeTextureMods nativeTextures = nativePreparation.Textures;
        IReadOnlyList<ModDiagnostic> nativeTextureDiagnostics = nativePreparation.Diagnostics;
        resolvers.Add(nativeTextures);
        usable.AddRange(nativeTextures.Variants.Select(variant => variant.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(localization!.Layers.Select(layer => layer.PackageId));
        usable.AddRange(guardianMods.Layers.Select(layer => layer.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(encounterMods.Layers.Select(layer => layer.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(modelMods.Variants.Select(variant => variant.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(springMods.PackageIds);
        usable.AddRange(sceneMods.Patches.Select(patch => patch.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(catalog.Files.Where(file => file.Format == ModFormat.Junction && !file.RelativePath.StartsWith("voice/", StringComparison.OrdinalIgnoreCase) && !(JunctionEncodedResources.Address(file.RelativePath) is ResourceKey key && JunctionEncodedResources.FieldGroup(key) is not null) && !JunctionEncodedResources.IsFieldMetadata(file.RelativePath) && !JunctionEncodedResources.IsUnusedFieldMember(file.RelativePath) && !JunctionEncodedResources.IsAncillaryFile(file.RelativePath)).Select(file => file.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(encodedResources.Select(resource => catalog.Files.First(file => $"{file.PackageId}:{file.RelativePath}" == resource.Content.Provenance).PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(behaviorPackages.Select(package => package.Id));
        usable.AddRange(statusMods.PackageIds);
        int itemCount = catalog.Files.Any(ItemOverrideMods.IsItemDocument) ? dataCandidate.Read(new ResourceKey("menu/mitem.bin")).Length / 4 : 0;
        if (!ItemOverrideMods.TryPrepare(catalog, itemCount, out ItemOverrideMods itemOverrides, out IReadOnlyList<ModDiagnostic> itemDiagnostics))
        {
            return ModPreparationResult.RejectPackagePayloads(itemDiagnostics);
        }
        usable.AddRange(itemOverrides.PackageIds);
        if (gameplayPolicy.ExtendedRosterEnabled)
        {
            usable.Add(GameplayTuningPolicy.ExtendedRosterPackageId);
        }
        if (gameplayPolicy.LevelBasedStatBonusesEnabled)
        {
            usable.Add(GameplayTuningPolicy.LevelBasedStatBonusesPackageId);
        }
        if (catalog.Files.Any(file => file.PackageId.Equals(ModRuntime.Ff7EnemyIntegrationPackageId, StringComparison.OrdinalIgnoreCase) && file.RelativePath.StartsWith(Ff7EnemyExtensionMods.PayloadDirectory, StringComparison.OrdinalIgnoreCase) && file.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            usable.Add(ModRuntime.Ff7EnemyIntegrationPackageId);
        }
        DemasterInspection demaster = DemasterTextureResolver.Inspect(source.Descriptor, catalog, (source as SelectedModelResourceSource)?.ModelSourceDescriptor);
        if (catalog.Files.Any(file => file.Format == ModFormat.Demaster))
        {
            resolvers.Add(new DemasterTextureResolver(catalog));
            usable.AddRange(demaster.UsablePackageIds);
        }
        using ResourceSourceView candidateSource = new(source, encodedResources, resourceLanguage);
        DemasterInspection ffnx = FfnxTextureResolver.Inspect(source.Descriptor, catalog, candidateSource, Resources.GetSource<IGameNativeMemorySource>());
        if (catalog.Files.Any(file => file.Format == ModFormat.Junction && (file.RelativePath.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase) || FfnxTextureResolver.IsNativeFieldBackgroundFamilyPath(file.RelativePath) || FfnxTextureResolver.IsNativeMenuArtworkFamilyPath(file.RelativePath))))
        {
            resolvers.Add(new FfnxTextureResolver(catalog));
            usable.AddRange(ffnx.UsablePackageIds);
        }
        TonberryTextureResolver? tonberry = TonberryTextureResolver.Create(source.Descriptor, catalog, out IReadOnlyList<ModDiagnostic> tonberryDiagnostics, recognition);
        IReadOnlyList<ModDiagnostic> diagnostics = encodedDiagnostics.Concat(statusDiagnostics).Concat(modelDiagnostics).Concat(fieldResourceDiagnostics).Concat(staticFileDiagnostics).Concat(tonberryDiagnostics).Concat(demaster.Diagnostics).Concat(ffnx.Diagnostics).Concat(nativeTextureDiagnostics).Concat(subtitleDiagnostics).Concat(guardianPublication.Diagnostics ?? []).Concat(encounterPublication.Diagnostics ?? []).ToArray();
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
        Dictionary<string, LocalizedTextAlias> textAliases = new(StringComparer.Ordinal);
        if (!statusMods.Names.IsEmpty)
        {
            NativeLocalizationTextReferences references = new(dataCandidate);
            foreach (StatusNameOverride name in statusMods.Names)
            {
                try
                {
                    string key = name.Reference.ResolveKey(Resources.Source.Language);
                    string original = references.ResolveOriginal(key);
                    foreach (string alias in SystemTextCatalog.KeysForSemanticId(BattleStatusLocalization.SemanticId(name.Status)))
                    {
                        textAliases.Add(alias, new(key, original));
                    }
                }
                catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or NotSupportedException)
                {
                    return new ModPreparationResult(false, Diagnostics: [new ModDiagnostic("Memoria.InvalidStatusNameReference", exception.Message, name.Source.PackageId, name.Source.RelativePath)]);
                }
            }
        }
        try
        {
            IReadOnlyList<TextureBuildContext> currentContexts = Resources.CaptureTextureContextsForPreparation();
            if (!currentContexts.SequenceEqual(candidate.Contexts, ReferenceEqualityComparer.Instance))
            {
                DiscardCandidate(candidate);
                return new(false) { Deferred = true };
            }
            candidate.Resources = Resources.BeginPrepare(resolver, textResources, encodedResources, ComposeAuthoringLocalization(localization!.Catalog), textAliases);
        }
        catch (ResourceReloadRequiredException exception)
        {
            return new ModPreparationResult(false, Diagnostics: encodedDiagnostics.Concat(fieldResourceDiagnostics).Append(new ModDiagnostic("resource-reload-required", exception.Message)).ToArray());
        }
        catch (InvalidDataException exception) when (!statusMods.Names.IsEmpty)
        {
            return new ModPreparationResult(false, Diagnostics: statusMods.Names.Select(name => new ModDiagnostic("Memoria.InvalidStatusNameReference", exception.Message, name.Source.PackageId, name.Source.RelativePath)).ToArray());
        }
        candidate.Finish = prepared =>
        {
            TexturePublication? texturePublication = PrepareTexturePublication?.Invoke(prepared);
            return new ModPreparationResult(true, usable, diagnostics, () =>
            {
                System.Diagnostics.Stopwatch publicationClock = System.Diagnostics.Stopwatch.StartNew();
                localization!.InputRevision?.ValidatePublication();
                prepared.Commit(notify: false, beforePublish: () =>
                {
                    guardianPublication.Commit?.Invoke();
                    encounterPublication.Commit?.Invoke();
                    texturePublication?.Commit();
                });
                CommittedLocalizations?.InputRevision?.Dispose();
                CommitLocalizationLanguage(localization);
                CommittedLocalizations = localization;
                GuardianForceMods = guardianMods;
                GuardianRules = guardianRules;
                EncounterMods = encounterMods;
                SceneMods = sceneMods;
                modelPublication = new(modelMods, springMods);
                nativeTextureMods = nativeTextures;
                movieSubtitles = subtitles;
                ItemOverrides = itemOverrides;
                StartupPolicy = startupPolicy;
                GameplayPolicy = gameplayPolicy;
                LastLocalizationChangedKeys = localization.ChangedKeys;
                LastLocalizationPublicationMilliseconds = publicationClock.Elapsed.TotalMilliseconds;
                VoiceResolver = voiceResolver;
            }, prepared.Notify) { Discard = texturePublication is null ? null : texturePublication.Dispose };
        };
        return AdvanceCandidate(candidate);
    }

    public void Dispose()
    {
        candidatePreparation?.Dispose();
        modLibrary?.Dispose();
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
        voiceStore.Dispose();
        source.Dispose();
        foreach (ModDirectoryResourceLease lease in modLibraryLeases.Values)
        {
            lease.Dispose();
        }
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
