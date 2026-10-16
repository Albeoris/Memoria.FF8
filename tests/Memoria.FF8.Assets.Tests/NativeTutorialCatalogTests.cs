using System.Text.Json;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTutorialCatalogTests
{
    [Fact]
    public void OriginalTutorialsKeepTheirExactDescriptorsAndIndependentNativeParties()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        Assert.Equal(9, catalog.Tutorials.Length);
        Assert.Same(catalog, assets.ReadNativeTutorialCatalog());
        for (int id = 0; id < catalog.Tutorials.Length; id++)
        {
            NativeTutorialDefinition tutorial = catalog.Get(id);
            var party = catalog.CreateIsolatedParty(id);
            var limits = catalog.CreateIsolatedLimitProgress(id);
            party.Validate();
            limits.Validate();
            Assert.Equal(id, tutorial.Descriptor.Id);
            Assert.Equal((uint)(62 + id), tutorial.Descriptor.UnlockId);
            Assert.Equal(id == 8 ? new byte[] { 1, 0, 5 } : new byte[] { 0, 1, 4 }, party.CombatParty);
            Assert.Equal(party.InitOutSha256, limits.InitOutSha256);
            Assert.Equal((ushort)0x4F, limits.DuelFlags);
            Assert.Equal((byte)0x11, limits.AngeloFlags);
            Assert.All(limits.TrainingCounters, counter => Assert.Equal((byte)255, counter));
            Assert.NotEmpty(tutorial.Title);
            Assert.NotEmpty(tutorial.Texts);
            Assert.Equal((ushort)0x60C0, tutorial.Inputs[0]);
            Assert.Contains((ushort)0xF000, tutorial.Inputs);
            Assert.Equal(2048, tutorial.InputBytes.Length);
            string original = JsonSerializer.Serialize(catalog.CreateIsolatedParty(id));
            party.CombatParty[0] = 255;
            party.Records[0].MagicInventory[0] = new(56, 100);
            Assert.Equal(original, JsonSerializer.Serialize(catalog.CreateIsolatedParty(id)));
        }
        Assert.Equal("GF Junction", catalog.Get(0).Title);
        Assert.Equal("Switch", catalog.Get(8).Title);
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Get(9));
    }

    [Fact]
    public void NativeBrowserKeepsEveryOriginalCategoryRangeAndTextResource()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialBrowserCatalog catalog = assets.ReadNativeTutorialBrowserCatalog();
        Assert.Equal(7, catalog.Categories.Length);
        Assert.Same(catalog, assets.ReadNativeTutorialBrowserCatalog());
        Assert.Equal(new byte[] { 25, 255, 26, 23, 23, 31, 21 }, catalog.Categories.Select(category => category.CallbackMode));
        Assert.Equal(new[] { "Battle Operation", "Online Help", "Card Game Rules", "TEST", "Review", "Icon Explanation", "Information" }, catalog.Categories.Select(category => category.Title));
        Assert.Equal(Enumerable.Range(43, 8), catalog.Categories[0].Pages.Select(page => (int)page.Id));
        Assert.Equal(Enumerable.Range(51, 13), catalog.Categories[2].Pages.Select(page => (int)page.Id));
        Assert.Equal(Enumerable.Range(64, 4), catalog.Categories[5].Pages.Select(page => (int)page.Id));
        Assert.Equal(25, catalog.Categories.Sum(category => category.Pages.Length));
        foreach (NativeTutorialBrowserPage page in catalog.Categories.SelectMany(category => category.Pages))
        {
            Assert.Equal(68, page.Metadata.Length);
            Assert.NotEmpty(page.ContentBytes);
            Assert.NotEmpty(page.TextBytes);
            Assert.NotEmpty(page.Placements);
            Assert.All(page.Placements, placement => Assert.Equal(page.Texts[placement.TextIndex].Value, placement.Value));
        }
        Assert.Equal("Status Window", catalog.Categories[0].Pages[0].Placements[0].Value);
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original tutorial resources are required.");
        return new GameAssets(directory);
    }
}
