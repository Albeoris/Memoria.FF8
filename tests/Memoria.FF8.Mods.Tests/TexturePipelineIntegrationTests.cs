using System.Numerics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using ImageMagick;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class TexturePipelineIntegrationTests
{
    [Fact]
    public void FieldRasterCacheFollowsPublishedImageDimensionsWithoutSceneReload()
    {
        using InMemoryResourceSource source = new(new("test", GameDataEdition.Classic, ["test"]));
        ResourceService resources = new(source);
        PixelImage original = new(16, 16, Enumerable.Repeat(new Rgba32(255, 255, 255), 256).ToArray());
        AssetHandle<TextureValue> handle = resources.GetTexture(new(new(new("field/example.mim"), "tile/0"), source.Descriptor, 1, "test", 16, 16, () => original));
        BackgroundTile tile = new(-16, -16, 0, 255, 0, 0, MapBlendMode.None, false, true, original) { Texture = handle };
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 1024), Vector2.Zero, 256);
        FieldAssets field = new(0, "example", [], [], [tile], camera, new FieldWalkmesh([]), []);
        FieldRenderer existing = new(field);
        byte[] baseline = existing.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        PixelImage hd = new(64, 64);
        for (int y = 0; y < hd.Height; y++)
        {
            for (int x = 0; x < hd.Width; x++)
            {
                hd[x, y] = new Rgba32((byte)(x * 4), (byte)(y * 4), 0);
            }
        }
        resources.Prepare(new Resolver(_ => new(hd, "test replacement", 16, 16))).Commit();
        byte[] reloaded = existing.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] fresh = new FieldRenderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.NotEqual(baseline, reloaded);
        Assert.Equal(fresh, reloaded);
        resources.Prepare(null).Commit();
        Assert.Equal(baseline, existing.Render(new Dictionary<int, int>(), [], TimeSpan.Zero));
    }

    [Fact]
    public void ClassicFieldAndBattleImagesRemainIdenticalThroughTheUnmoddedResourcePipeline()
    {
        string directory = InstalledGame();
        using ClassicGameDataSource source = new(directory);
        ResourceService resources = new(source);
        using GameAssets original = new(directory);
        using GameAssets routed = new(directory, resources: resources);
        FieldAssets before = original.LoadField(252);
        FieldAssets after = routed.LoadField(252);
        Assert.Equal(before.Background.Count, after.Background.Count);
        for (int index = 0; index < before.Background.Count; index++)
        {
            Assert.Equal(before.Background[index].Image.Pixels, after.Background[index].ResolvedImage.Pixels);
        }
        for (int model = 0; model < before.Models.Count; model++)
        {
            for (int image = 0; image < before.Models[model].Textures.Count; image++)
            {
                Assert.Equal(before.Models[model].Textures[image].Pixels, after.Models[model].Textures[image].Pixels);
            }
        }
        Assert.Equal(new FieldRenderer(before).Render(new Dictionary<int, int>(), [], TimeSpan.Zero), new FieldRenderer(after).Render(new Dictionary<int, int>(), [], TimeSpan.Zero));
        using BattleAssets originalBattle = new(directory);
        using BattleAssets routedBattle = new(directory, resources);
        BattleStage originalStage = originalBattle.LoadStage(0);
        BattleStage routedStage = routedBattle.LoadStage(0);
        BattleActorDraw[] Actors(BattleAssets assets) => [new(assets.LoadPlayer(0), new Vector3(-12, 0, 0)), new(assets.LoadEnemy(0), new Vector3(12, 0, 0), MathF.PI)];
        Assert.Equal(new BattleRenderer(320, 180).Render(originalStage, Actors(originalBattle)), new BattleRenderer(320, 180).Render(routedStage, Actors(routedBattle)));
    }

    [Fact]
    public void BothInstalledHorizonPacksRenderOnTheSameClassicWorldAndRestoreWithoutRecreatingIt()
    {
        string directory = InstalledGame();
        string tonberryRoot = Path.Combine(directory, "Mods/Tonberry/HorizonPackRemaster_v1-0");
        string demasterRoot = Path.Combine(directory, "Mods/Demaster/HorizonPack-Remastered_v1-1a");
        Assert.SkipUnless(Directory.Exists(tonberryRoot) && Directory.Exists(demasterRoot), "Both installed HorizonPack packages are required.");
        using ClassicGameDataSource source = new(directory);
        ResourceService resources = new(source);
        using WorldAssets original = new(directory);
        using WorldAssets world = new(directory, resources);
        WorldTerrainRenderer renderer = new(world, 640, 448);
        WorldTerrainRenderer baselineRenderer = new(original, 640, 448);
        WorldSpawn spawn = world.Spawns[0];
        Vector3 position = new(spawn.X, -spawn.NativeHeight, spawn.Z);
        WorldCamera camera = new(0.5f, 200, 120, 0, 0.8f, 1600);
        byte[] Render(WorldTerrainRenderer target) => target.Render(position, 0, 0, 0, camera, avatarVisible: false).ToArray();
        byte[] baseline = Render(renderer);
        Assert.Equal(Render(baselineRenderer), baseline);
        Assert.True(renderer.DrawnTriangles > 100);
        ModCatalogSnapshot catalog = new(1, [], Files(tonberryRoot, ModFormat.Tonberry).Concat(Files(demasterRoot, ModFormat.Demaster)));
        TonberryTextureResolver tonberry = Assert.IsType<TonberryTextureResolver>(TonberryTextureResolver.Create(source.Descriptor, catalog, out _));
        resources.Prepare(tonberry).Commit();
        byte[] tonberryFrame = Render(renderer);
        Assert.NotEqual(baseline, tonberryFrame);
        resources.Prepare(new OrderedTextureResolver(new DemasterTextureResolver(catalog), tonberry)).Commit();
        byte[] demasterFrame = Render(renderer);
        Assert.NotEqual(baseline, demasterFrame);
        resources.Prepare(tonberry).Commit();
        Assert.Equal(tonberryFrame, Render(renderer));
        resources.Prepare(null).Commit();
        Assert.Equal(baseline, Render(renderer));
        string? evidence = Environment.GetEnvironmentVariable("FF8_TEXTURE_EVIDENCE_DIR");
        if (evidence is not null)
        {
            Directory.CreateDirectory(evidence);
            WritePng(Path.Combine(evidence, "world-base.png"), baseline, 640, 448);
            WritePng(Path.Combine(evidence, "world-tonberry.png"), tonberryFrame, 640, 448);
            WritePng(Path.Combine(evidence, "world-demaster.png"), demasterFrame, 640, 448);
        }
    }

    private static string InstalledGame()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed Classic corpus is required.");
        return directory;
    }

    private static IEnumerable<ModFile> Files(string root, ModFormat format) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Select(path => new ModFile($"{format}/{Path.GetFileName(root)}", format, Path.GetRelativePath(root, path), 1, () => File.ReadAllBytes(path)));

    private static void WritePng(string path, byte[] pixels, int width, int height)
    {
        using MagickImage image = new(pixels, new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA));
        image.Write(path, MagickFormat.Png);
    }

    private sealed class Resolver(Func<TextureBuildContext, TextureReplacement?> resolve) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => resolve(context);
    }
}
