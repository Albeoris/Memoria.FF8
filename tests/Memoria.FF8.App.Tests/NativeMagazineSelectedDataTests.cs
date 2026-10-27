using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMagazineSelectedDataTests
{
    [Theory]
    [InlineData("menu-data-a")]
    [InlineData("menu-data-b")]
    [InlineData("menu-data-c")]
    public void OptionalSelectedMagazineMetadataAndDependenciesReachTheLiveCatalog(string fixture)
    {
        string catalog = TestResources.FixturePath(fixture);
        Dictionary<string, string> files = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalog))!;
        byte[] Read(string name) => File.ReadAllBytes(Path.GetFullPath(files[name], Path.GetDirectoryName(catalog)!));
        string[] selectedNames = ["mmag.bin", "mitem.bin", "mwepon.bin", "mwepon.msg", "mngrp.bin", "mngrphd.bin", "pet_exp.bin", "pet_exp.msg", "magita.TEX"];
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        resources.Prepare(null, encodedResources: selectedNames.Select(name => new EncodedResource(new ResourceKey("menu/" + name), Path.GetExtension(name).TrimStart('.'), 1, "fixture", Read(name)))).Commit();
        using GameAssets assets = new(TestResources.GameDirectory, resources: resources);
        LiveMenuCatalog menu = LiveMenuCatalog.ReadInstalled(assets);
        byte[] items = Read("mitem.bin");
        byte[] metadata = Read("mmag.bin");
        int pageCount = 0;
        for (int item = 1; item < 199; item++)
        {
            if (items[item * 4] != 9)
            {
                continue;
            }
            int first = items[item * 4 + 2];
            int last = items[item * 4 + 3];
            var pages = menu.MagazinePages(item);
            Assert.Equal(Enumerable.Range(first, last - first + 1), pages.Select(page => (int)page.PageId));
            foreach (var page in pages)
            {
                Assert.NotNull(page.Layout);
                Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(metadata.AsSpan(page.PageId * 68)), page.Layout!.X);
                Assert.Equal(metadata[page.PageId * 68 + 24], page.WeaponRecipe);
            }
            pageCount += pages.Length;
        }
        Assert.Equal(43, pageCount);
    }
}
