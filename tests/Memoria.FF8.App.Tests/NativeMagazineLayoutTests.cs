using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMagazineLayoutTests
{
    [Fact]
    public void EveryInventoryPageRetainsNativePlacementAndArtworkIdentity()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeMagazinePage[] pages = Enumerable.Range(177, 22).SelectMany(catalog.MagazinePages).ToArray();
        Assert.Equal(43, pages.Length);
        Assert.All(pages, page => Assert.NotNull(page.Layout));
        Assert.Equal(Enumerable.Range(0, 40), pages.SelectMany(page => page.Layout!.Images).Select(image => image.ImageId).Order());
        NativeMagazineLayout weapon = pages[0].Layout!;
        Assert.Equal((24, 8, 336, 184), (weapon.X, weapon.Y, weapon.Width, weapon.Height));
        Assert.Equal((13, 6), (weapon.Texts[0].X, weapon.Texts[0].Y));
        Assert.Equal((25, 84, 0, 128, 104), (weapon.Images[0].X, weapon.Images[0].Y, weapon.Images[0].ImageId, weapon.Images[0].Width, weapon.Images[0].Height));
        Assert.Contains(weapon.Texts, text => text.Text == "Adamantine" && text.X == 183 && text.Y == 102);
        Assert.NotEmpty(weapon.Icons);
        Assert.All(pages.Where(page => page.DuelMove != 255), page =>
        {
            Assert.NotEmpty(page.Layout!.Buttons);
            Assert.Equal(page.Layout.Buttons.Length - 1, page.Layout.Icons.Count(icon => icon.IconId == 55));
        });
        Assert.Equal(184, pages[40].Layout!.Images[0].Width);
        Assert.Equal(new[] { 39 }, pages[41].Layout!.Images.Select(image => image.ImageId));
        Assert.Equal(new[] { 37, 38 }, pages[42].Layout!.Images.Select(image => image.ImageId));
        Assert.All(pages, page => Assert.InRange(page.Layout!.PaperColor.Red, (byte)140, (byte)155));
        NativeMagazinePage copy = catalog.MagazinePage(177, 0);
        copy.Layout!.Texts[0] = new NativeMagazineText(0, 0, "mutated");
        Assert.Equal(13, catalog.MagazinePage(177, 0).Layout!.Texts[0].X);
    }

    private static GameAssets Assets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original magazine resources are required.");
        return new GameAssets(directory);
    }
}
