using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

/// <summary>Application composition root. Resource consumers do not reference the mod adapters.</summary>
internal sealed class GameResourceHost : IDisposable
{
    private readonly ClassicGameDataSource source;
    private TonberryRecognitionCache? recognition;

    public GameResourceHost(string gameDirectory, string? applicationBaseDirectory = null, bool watch = true)
    {
        source = new ClassicGameDataSource(gameDirectory);
        Resources = new ResourceService(source);
        Mods = new ModRuntime(applicationBaseDirectory, format => ModCompatibility.IsCompatible(source.Descriptor.Edition, format), Prepare, watch);
        Resources.ReplacementFailed += (key, exception) => Mods.ReportFailure(new ModDiagnostic("replacement-failed", $"{key}: {exception.Message} Original pixels remain active.", (exception as ModTextureLoadException)?.PackageId, (exception as ModTextureLoadException)?.RelativePath));
        Pump(true);
    }

    public ResourceService Resources { get; }
    public ModRuntime Mods { get; }

    public bool Pump(bool force = false)
    {
        Mods.PreparePending(force);
        return Mods.CommitPending();
    }

    private ModPreparationResult Prepare(ModCatalogSnapshot catalog)
    {
        List<ITextureReplacementResolver> resolvers = [];
        List<string> usable = [];
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
        return new ModPreparationResult(true, usable, diagnostics, () => prepared.Commit(notify: false), prepared.Notify);
    }

    public void Dispose()
    {
        Mods.Dispose();
        recognition?.Clear();
        source.Dispose();
    }
}
