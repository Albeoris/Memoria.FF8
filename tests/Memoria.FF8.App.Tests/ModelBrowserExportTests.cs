using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class ModelBrowserExportTests
{
    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR", "Classic")]
    [InlineData("MEMORIA_FF8_REMASTERED_GAME_DIR", "Remastered")]
    public async Task OriginalFieldModelExportsItsLinkedClipSelection(string gameDirectoryVariable, string edition)
    {
        string? game = Environment.GetEnvironmentVariable(gameDirectoryVariable);
        Assert.SkipUnless(Directory.Exists(game), "Set the optional original game directory.");
        using GameAssets assets = new(game);
        Assert.Equal(edition, assets.Resources.Source.Edition.ToString());
        const string id = "field/test7/4";
        ModelCatalogEntry entry = new(id, "Field model", "Field", "field/test7/chara.one", "test7", ModelIndex: 4);
        ModelAssetDiscoveryItem[] selection = assets.DiscoverModelAssetsForExchange(entry, TestContext.Current.CancellationToken)
            .Where(item => item.Error is null && (item.Kind == ModelAssetDiscoveryKind.Object && item.Id == id || item.Kind == ModelAssetDiscoveryKind.Clip && item.OwnerId == id))
            .ToArray();
        Assert.True(selection.Length > 1000);
        EditableModel full = assets.LoadCatalogModel(entry);
        EditableModel retained = assets.LoadCatalogModelForExchange(entry);
        Assert.True(full.NativeMetadata.TryGetProperty("Textures", out _));
        Assert.False(retained.NativeMetadata.TryGetProperty("Textures", out _));
        Assert.Equal(RecreateGame.BrowserSourceFingerprint(full, retainedMetadata: true), RecreateGame.BrowserSourceFingerprint(retained, retainedMetadata: true));
        string path = Path.Combine(Path.GetTempPath(), $"memoria-model-export-{Guid.NewGuid():N}.glb");
        try
        {
            await RecreateGame.ExportBrowserSelectionAsync(path, selection, "original", ModelMods.Empty, edition, [], TestContext.Current.CancellationToken);
            ModelAssetImportInventory result = ModelAssetExchange.InspectGlb(File.ReadAllBytes(path));
            Assert.Equal(edition, result.Edition);
            Assert.Single(result.Objects, item => item.GeometrySelected);
            Assert.Equal(selection.Length - 1, result.Clips.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlowModelLoadLeavesDialogContinuationsResponsiveAndPublishesOnlyOnSuccess(bool cancel)
    {
        string path = Path.Combine(Path.GetTempPath(), $"memoria-model-export-{Guid.NewGuid():N}.glb");
        byte[] previous = [1, 2, 3];
        File.WriteAllBytes(path, previous);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using ManualResetEventSlim releaseLoader = new();
        using GameDialogContext dialog = new();
        TaskCompletionSource loaderEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        const string target = "field/example/0";
        EditableAnimation animation = new("Clip", [new(0, "translation", [0, 0.1f], [Vector4.Zero, Vector4.UnitX])]);
        EditableVertex Vertex(Vector3 position) => new(position, Vector3.UnitZ, Vector2.Zero, Vector4.Zero, Vector4.UnitX, Vector4.One);
        EditableModel Load()
        {
            loaderEntered.TrySetResult();
            if (!releaseLoader.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken))
            {
                throw new TimeoutException("The export loader was never released.");
            }
            return new("Model", "field", [new EditableBone("bone_0", -1, Vector3.Zero, Quaternion.Identity, Vector3.One) { NativeIndex = 0 }], [new EditableMesh("geometry", [Vertex(Vector3.Zero), Vertex(Vector3.UnitX), Vertex(Vector3.UnitY)], [0, 1, 2], -1, Matrix4x4.Identity) { Skinned = true }], [], [animation], JsonSerializer.SerializeToElement(new { }));
        }
        ModelAssetDiscoveryItem geometry = new(target, "Object", ModelAssetDiscoveryKind.Object, target, "field/example/chara.one", "embedded", -1, "test", Load);
        ModelAssetDiscoveryItem clip = geometry with { Id = target + "/clips/embedded/0", Kind = ModelAssetDiscoveryKind.Clip, ClipIndex = 0 };
        Task export = RecreateGame.ExportBrowserSelectionAsync(path, [geometry, clip], "original", ModelMods.Empty, "Classic", [], cancellation.Token);
        try
        {
            await loaderEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(export.IsCompleted);
            bool callbackRan = false;
            dialog.Post(_ => callbackRan = true, null);
            dialog.Drain();
            Assert.True(callbackRan);
            Assert.Equal(previous, File.ReadAllBytes(path));
            if (cancel)
            {
                cancellation.Cancel();
                releaseLoader.Set();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export);
                Assert.Equal(previous, File.ReadAllBytes(path));
            }
            else
            {
                releaseLoader.Set();
                await export.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                ModelAssetImportInventory result = ModelAssetExchange.InspectGlb(File.ReadAllBytes(path));
                ModelAssetObject exported = Assert.Single(result.Objects);
                Assert.Equal(target, exported.Target);
                Assert.Equal("Original", exported.SourceVariant);
                Assert.Equal(RecreateGame.BrowserSourceFingerprint(Load(), retainedMetadata: true), exported.Fingerprint);
                Assert.Equal(clip.Id, Assert.Single(result.Clips).Target);
            }
        }
        finally
        {
            releaseLoader.Set();
            try
            {
                await export.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
