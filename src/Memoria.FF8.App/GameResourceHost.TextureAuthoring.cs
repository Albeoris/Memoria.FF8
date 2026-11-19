using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    public string TextureAuthoringScopeDescription => source is SelectedModelResourceSource selected
        ? $"FFVIII / {source.Descriptor.Edition} resources / {selected.ModelSourceDescriptor.Edition} models / {Resources.ResourceLanguage}"
        : $"FFVIII / {source.Descriptor.Edition} / {Resources.ResourceLanguage}";

    /// <summary>Capture on the owner thread, invoke on a worker. The worker gets independent archive readers and resolver caches.</summary>
    public Func<TextureAuthoringSession> CaptureTextureAuthoringFactory(bool resolved)
    {
        ModCatalogSnapshot catalog = Mods.Catalog;
        var localization = Resources.Localization;
        IReadOnlyList<EncodedResource> encoded = Resources.CaptureEncodedResources();
        string language = Resources.ResourceLanguage;
        return () =>
        {
            IResourceSource isolated = GameDataSourceFactory.OpenTextureAuthoring(gameDirectory, modelSourceDirectory, language);
            try
            {
                ResourceService resources = new(isolated, language);
                if (resolved)
                {
                    if (!NativeTextureMods.TryPrepare(catalog, out NativeTextureMods native, out IReadOnlyList<ModDiagnostic> errors))
                    {
                        throw new InvalidDataException(string.Join(Environment.NewLine, errors.Select(error => error.Message)));
                    }
                    List<ITextureReplacementResolver> resolvers = [native];
                    if (catalog.Files.Any(file => file.Format == ModFormat.Demaster))
                    {
                        resolvers.Add(new DemasterTextureResolver(catalog));
                    }
                    if (catalog.Files.Any(file => file.Format == ModFormat.Junction))
                    {
                        resolvers.Add(new FfnxTextureResolver(catalog));
                    }
                    TonberryTextureResolver? tonberry = TonberryTextureResolver.Create(isolated.Descriptor, catalog, out IReadOnlyList<ModDiagnostic> tonberryErrors);
                    if (tonberryErrors.Any(error => error.Code == "Tonberry.MalformedRow"))
                    {
                        throw new InvalidDataException(string.Join(Environment.NewLine, tonberryErrors.Select(error => error.Message)));
                    }
                    if (tonberry is not null)
                    {
                        resolvers.Add(tonberry);
                    }
                    resources.Prepare(new OrderedTextureResolver(resolvers.ToArray()), encodedResources: encoded, localization: localization).Commit();
                }
                return new TextureAuthoringSession(new GameAssets(gameDirectory, resources: resources), isolated);
            }
            catch
            {
                isolated.Dispose();
                throw;
            }
        };
    }
}

internal sealed class TextureAuthoringSession(GameAssets assets, IResourceSource source) : IDisposable
{
    public GameAssets Assets { get; } = assets;

    public void Dispose()
    {
        Assets.Dispose();
        source.Dispose();
    }
}
