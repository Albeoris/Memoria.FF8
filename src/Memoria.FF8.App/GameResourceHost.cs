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
    private GameAssets? sceneValidationAssets;

    public GameResourceHost(string gameDirectory, string? applicationBaseDirectory = null, bool watch = true, string? modsDirectory = null)
    {
        this.gameDirectory = gameDirectory;
        source = GameDataSourceFactory.Open(gameDirectory);
        Resources = new ResourceService(source);
        Mods = new ModRuntime(applicationBaseDirectory, format => ModCompatibility.IsCompatible(source.Descriptor.Edition, format), Prepare, watch, additionalBaseDirectories: [gameDirectory], modsDirectory: modsDirectory);
        Resources.ReplacementFailed += (key, exception) => Mods.ReportFailure(new ModDiagnostic("replacement-failed", $"{key}: {exception.Message} Original pixels remain active.", (exception as ModTextureLoadException)?.PackageId, (exception as ModTextureLoadException)?.RelativePath));
        Pump(true);
    }

    public ResourceService Resources { get; }
    public ModRuntime Mods { get; }
    public FieldSceneMods SceneMods { get; private set; } = FieldSceneMods.Empty;
    public ModelMods ModelMods { get; private set; } = ModelMods.Empty;

    public FieldModel Apply(string modelId, FieldModel original) => ModelMods.Apply(modelId, original);
    public BattleModel Apply(string modelId, BattleModel original) => ModelMods.Apply(modelId, original);
    public BattleStage Apply(string modelId, BattleStage original) => ModelMods.Apply(modelId, original);
    public WorldStaticModel Apply(string modelId, WorldStaticModel original) => ModelMods.Apply(modelId, original);

    public string ResolveGameFile(string dataRelativePath)
    {
        string relative = ModCatalogSnapshot.NormalizePath(dataRelativePath);
        ModFile? replacement = Mods.Catalog.FindAll(ModFormat.Junction, relative).FirstOrDefault();
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

    public bool Pump(bool force = false)
    {
        Mods.PreparePending(force);
        return Mods.CommitPending();
    }

    private ModPreparationResult Prepare(ModCatalogSnapshot catalog)
    {
        if (!ModelMods.TryPrepare(catalog, out ModelMods modelMods, out IReadOnlyList<ModDiagnostic> modelDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: modelDiagnostics);
        }
        if (!FieldSceneMods.TryPrepare(catalog, out FieldSceneMods sceneMods, out IReadOnlyList<ModDiagnostic> sceneDiagnostics))
        {
            return new ModPreparationResult(false, Diagnostics: sceneDiagnostics);
        }
        IReadOnlyList<ModDiagnostic> sceneResourceDiagnostics = ValidateSceneModResources(sceneMods);
        if (sceneResourceDiagnostics.Count != 0)
        {
            return new ModPreparationResult(false, Diagnostics: sceneResourceDiagnostics);
        }
        List<ITextureReplacementResolver> resolvers = [];
        List<string> usable = [];
        usable.AddRange(modelMods.Variants.Select(variant => variant.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(sceneMods.Patches.Select(patch => patch.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        usable.AddRange(catalog.Files.Where(file => file.Format == ModFormat.Junction).Select(file => file.PackageId).Distinct(StringComparer.OrdinalIgnoreCase));
        DemasterInspection demaster = DemasterTextureResolver.Inspect(source.Descriptor, catalog);
        if (catalog.Files.Any(file => file.Format == ModFormat.Demaster))
        {
            resolvers.Add(new DemasterTextureResolver(catalog));
            usable.AddRange(demaster.UsablePackageIds);
        }
        TonberryTextureResolver? tonberry = TonberryTextureResolver.Create(source.Descriptor, catalog, out IReadOnlyList<ModDiagnostic> tonberryDiagnostics, recognition);
        IReadOnlyList<ModDiagnostic> diagnostics = tonberryDiagnostics.Concat(demaster.Diagnostics).ToArray();
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
        ResourceService.PreparedResources prepared = Resources.Prepare(resolver);
        return new ModPreparationResult(true, usable, diagnostics, () =>
        {
            prepared.Commit(notify: false);
            SceneMods = sceneMods;
            ModelMods = modelMods;
        }, prepared.Notify);
    }

    public void Dispose()
    {
        Mods.Dispose();
        recognition?.Clear();
        sceneValidationAssets?.Dispose();
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
