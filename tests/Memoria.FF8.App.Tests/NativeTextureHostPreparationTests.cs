using System.Diagnostics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTextureHostPreparationTests
{
    [Fact]
    public void InitialPackagesAreReadyAndResidentRefreshYieldsBeforeAtomicPublication()
    {
        string game = TestResources.GameDirectory;
        string root = Path.Combine(Path.GetTempPath(), "memoria-native-host-" + Guid.NewGuid().ToString("N"));
        try
        {
            string mods = Path.Combine(root, "Mods");
            string package = Path.Combine(mods, "Memoria", "synthetic");
            Directory.CreateDirectory(package);
            File.WriteAllText(Path.Combine(package, "mod.json"), """{"id":"synthetic-textures","name":"Synthetic textures","package":"Memoria","description":{"en":"Procedural test artwork"}}""");
            File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled=1\npriority=999\n");
            TextureKey key = new(new("texture-authoring/synthetic.tim"), "image");
            string imagePath = Path.Combine(package, TextureTarget.FromKey(key).CanonicalPath().Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            Rgba32 first = new(10, 20, 30, 255);
            File.WriteAllBytes(imagePath, Png(first));
            using GameResourceHost host = new(game, Path.Combine(root, "Application"), watch: true, modsDirectory: mods);
            PixelImage original = new(1, 1, [new(90, 80, 70)]);
            TextureBuildContext context = new(key, new("synthetic", GameDataEdition.Classic, []), 1, "image", 1, 1, () => original);
            AssetHandle<TextureValue> handle = host.Resources.GetTexture(context);
            Assert.Equal(first, handle.Value.Image[0, 0]);
            List<AssetHandle<TextureValue>> unrelated = [];
            for (int index = 0; index < 96; index++)
            {
                TextureKey otherKey = new(key.Owner, "unrelated/" + index);
                unrelated.Add(host.Resources.GetTexture(new(otherKey, context.Source, 1, "image", 1, 1, () => original)));
            }
            int publications = 0;
            TextureValue previous = handle.Value;
            host.PrepareTexturePublication = prepared =>
            {
                if (!prepared.TextureValues.ContainsKey(key))
                {
                    return null!;
                }
                Assert.Same(previous, handle.Value);
                Assert.Contains(key, prepared.TextureValues.Keys);
                return new TexturePublication(() => publications++, () =>
                {
                });
            };
            long generation = host.Resources.Generation;
            Rgba32 second = new(40, 50, 60, 255);
            File.WriteAllBytes(imagePath, Png(second));
            host.Pump();
            Assert.Equal(generation, host.Resources.Generation);
            Assert.Same(previous, handle.Value);
            PumpUntil(host, () => handle.Value.Image[0, 0] == second);
            Assert.True(host.Resources.Generation > generation);
            Assert.Equal(second, handle.Value.Image[0, 0]);
            Assert.Equal(1, publications);
            Assert.All(unrelated, item => Assert.Same(original, item.Value.Image));

            previous = handle.Value;
            generation = host.Resources.Generation;
            File.WriteAllBytes(imagePath, [1, 2, 3]);
            host.Pump();
            PumpUntil(host, () => host.Mods.Diagnostics.Any(item => item.Code == "Memoria.InvalidTexture"));
            Assert.Equal(generation, host.Resources.Generation);
            Assert.Same(previous, handle.Value);
            Assert.Equal(1, publications);

            Rgba32 repaired = new(70, 80, 90, 255);
            File.WriteAllBytes(imagePath, Png(repaired));
            host.Pump();
            PumpUntil(host, () => handle.Value.Image[0, 0] == repaired);
            Assert.True(host.Resources.Generation > generation);
            Assert.Equal(repaired, handle.Value.Image[0, 0]);
            Assert.Equal(2, publications);
            Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "Memoria.InvalidTexture");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static byte[] Png(Rgba32 color) => LocalizationArtworkExporter.EncodePng(new(1, 1, [color]));

    private static void PumpUntil(GameResourceHost host, Func<bool> condition)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(10))
        {
            host.Pump();
            Thread.Sleep(2);
        }
        Assert.True(condition(), string.Join("; ", host.Mods.Diagnostics.Select(item => item.Code + ": " + item.Message)));
    }
}
