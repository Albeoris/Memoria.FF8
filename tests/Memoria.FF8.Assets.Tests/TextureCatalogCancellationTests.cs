using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class TextureCatalogCancellationTests
{
    [Fact]
    public void CancellationBeforeFirstMoveNextStopsModelFieldAndTextureDiscovery()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(directory), "An explicitly configured installed source is required.");
        using IResourceSource source = GameDataSourceFactory.OpenTextureAuthoring(directory!);
        using GameAssets assets = new(directory!, resources: new(source));
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        using IEnumerator<ModelCatalogEntry> models = assets.EnumerateModelCatalog(cancellation.Token).GetEnumerator();
        using IEnumerator<ModelCatalogEntry> fields = assets.EnumerateFieldModelSources(cancellation.Token).GetEnumerator();
        using IEnumerator<TextureCatalogEntry> textures = assets.EnumerateTextureCatalog(cancellation.Token).GetEnumerator();
        Assert.Throws<OperationCanceledException>(() => models.MoveNext());
        Assert.Throws<OperationCanceledException>(() => fields.MoveNext());
        Assert.Throws<OperationCanceledException>(() => textures.MoveNext());
        Assert.Equal(0, assets.Resources.ResidentTextureCount);
    }
}
