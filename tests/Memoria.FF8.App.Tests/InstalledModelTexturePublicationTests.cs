using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class InstalledModelTexturePublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CharacterAndWeaponTexturesPublishIntoTheExistingBattleRenderer(bool paintHd)
    {
        string game = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(game, Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_SOURCE_DIRECTORY"));
        ResourceService resources = new(source);
        using BattleAssets assets = new(game, resources: resources);
        BattleModel body = assets.LoadPlayer(0);
        BattleModel weapon = assets.LoadWeapon(0, assets.WeaponModelIds(0).First(), body);
        BattleActorDraw[] actors = [new(body, Vector3.Zero), new(weapon, Vector3.Zero)];
        BattleStage stage = assets.LoadStage(assets.StageIds.First());
        BattleRenderer renderer = new(640, 480);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(stage, actors);
        var before = GpuTestDevice.Read(gpu.Target);
        TextureKey[] keys = ((ResourceTextureList)body.Textures).TextureKeys.Concat(((ResourceTextureList)weapon.Textures).TextureKeys).Distinct().ToArray();
        var snapshots = resources.CaptureTextureSources(keys);
        Assert.NotEmpty(snapshots);
        List<ModFile> files = [];
        foreach (var snapshot in snapshots)
        {
            PixelImage image = snapshot.Original;
            if (paintHd)
            {
                image = new(image.Width * 2, image.Height * 2, Enumerable.Repeat(new Rgba32(255, 0, 255), image.Width * image.Height * 4).ToArray());
            }
            else if (TexturePixelExchange.UsesNativeFlags(snapshot.Context))
            {
                image = TexturePixelExchange.ToEditable(image);
            }
            byte[] png = LocalizationArtworkExporter.EncodePng(image);
            files.Add(new("Memoria/actor-textures", ModFormat.Memoria, snapshot.Target.CanonicalPath(), 1, () => png));
        }
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], files), out NativeTextureMods native, out var diagnostics), string.Join("; ", diagnostics.Select(error => error.Message)));
        ResourceService.PreparedResources candidate = resources.Prepare(native);
        using TexturePublication graphics = gpu.PrepareTexturePublication(candidate.TextureValues.Values.Select(value => value.Image), snapshots.Select(snapshot => snapshot.Resolved.Image));
        candidate.Commit(beforePublish: graphics.Commit);
        renderer.Render(stage, actors);
        var after = GpuTestDevice.Read(gpu.Target);
        if (paintHd)
        {
            Assert.Contains(after.Zip(before), pair => pair.First != pair.Second);
        }
        else
        {
            Assert.Equal(before, after);
        }
        Assert.Equal(0, gpu.FrameTextureUploads);
        Assert.True(renderer.DrawnTriangles > 0);
        Assert.Same(body, actors[0].Model);
        Assert.Same(weapon, actors[1].Model);
    }
}
