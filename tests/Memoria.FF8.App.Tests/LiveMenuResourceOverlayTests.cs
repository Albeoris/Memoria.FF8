using System.Buffers.Binary;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LiveMenuResourceOverlayTests
{
    [Fact]
    public void SelectedSourceFilesFeedMenusAndRetainLocalizationAndRestartContracts()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native menu resources are required.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        string language = resources.Source.Language;
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        KernelData kernel = KernelData.ReadFromArray(Read("main/kernel.bin"), encoding);
        kernel.MagicSpells[1].Name = "Selected spell";
        string[] locations = NamedicBin.ReadStrings(Read("menu/areames.dc1"), encoding);
        locations[7] = "Selected place";
        byte[] world = Read("world/wmsetus.obj");
        int locationStart = BinaryPrimitives.ReadInt32LittleEndian(world.AsSpan(18 * 4));
        world.AsSpan(locationStart, WorldAssets.Columns * WorldAssets.Rows).Fill(7);
        EncodedResource[] selected =
        [
            new(new ResourceKey("main/kernel.bin"), "bin", 1, "fixture", kernel.WriteToArray(encoding)),
            new(new ResourceKey("menu/areames.dc1"), "dc1", 1, "fixture", NamedicBin.WriteStrings(locations, encoding)),
            new(new ResourceKey("world/wmsetus.obj"), "obj", 1, "fixture", world)
        ];
        resources.Prepare(null, encodedResources: selected).Commit();
        using GameAssets assets = new(directory, resources: resources);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        Assert.Equal("Selected spell", catalog.MagicName(1));
        Assert.Equal("Selected place", catalog.PlaceName(7));
        GameState worldState = new() { Scene = SceneKind.WorldMap, World = new WorldState { X = 0, Z = 0 } };
        Assert.Equal(7, catalog.LocationId(worldState));
        Assert.Equal("Selected place", catalog.LocationName(worldState));
        Assert.Equal(language, resources.Source.Language);
        string key = LocalizationSourceExporter.IndexedKey(language, LocalizationSourceExporter.KernelTable("MagicSpells", "Name"), 1);
        resources.PrepareLocalization(new LocalizationCatalog(new Dictionary<string, LocalizationValue> { [key] = new("Presentation spell", "fixture", "fixture") })).Commit();
        Assert.Equal("Presentation spell", catalog.MagicName(1));
        resources.PrepareLocalization(LocalizationCatalog.Empty).Commit();
        Assert.Equal("Selected spell", catalog.MagicName(1));
        foreach (EncodedResource removed in selected)
        {
            ResourceReloadRequiredException failure = Assert.Throws<ResourceReloadRequiredException>(() => resources.Prepare(null, encodedResources: selected.Where(resource => resource.Key != removed.Key)));
            Assert.Contains(removed.Key, failure.Resources);
        }
        Assert.Equal("Selected spell", catalog.MagicName(1));
        Assert.Equal("Selected place", catalog.PlaceName(7));

        byte[] Read(string path)
        {
            using Stream input = source.Find(new ResourceKey(path))!.OpenRead();
            using MemoryStream bytes = new();
            input.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
