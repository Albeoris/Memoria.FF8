using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModPreviewTextureAtlasTests
{
    private const string Cards = "mods/textures/eng/cardgame/cards_00.png";
    private const string Icons = "mods/textures/eng/cardgame/icons_00.png";
    private const string OutcomePage = "mods/textures/eng/cardgame/icons_1_0_255.png";

    [Fact]
    public async Task ListsSheetAndPageMembersWithoutDecodingPackageImages()
    {
        await using PreviewFixture fixture = new();
        fixture.Write(Cards, []);
        fixture.Write(Icons, []);
        fixture.Write(OutcomePage, []);
        ModPreviewDocument document = await fixture.Open();

        ModPreviewEntry[] cards = document.Entries.Where(entry => entry.SourcePath == Cards).ToArray();
        Assert.Equal(111, cards.Length);
        Assert.Equal(cards.Length, cards.Select(entry => entry.Id).Distinct().Count());
        Assert.All(cards, entry => Assert.Equal("cards_00.png", entry.Folder));
        Assert.Contains(cards, entry => entry.Name == "Card 001");
        Assert.Contains(cards, entry => entry.Name == "Card 110");
        Assert.Contains(cards, entry => entry.Name == "Card back");

        ModPreviewEntry[] icons = document.Entries.Where(entry => entry.SourcePath == Icons).ToArray();
        Assert.Equal(14, icons.Length);
        Assert.Contains(icons, entry => entry.Name == "Fire");
        Assert.Contains(icons, entry => entry.Name == "Player wins");
        Assert.Contains(icons, entry => entry.Name == "Combo");
        Assert.Equal(new[] { "Draw", "Opponent wins", "Player wins" }, document.Entries.Where(entry => entry.SourcePath == OutcomePage).Select(entry => entry.Name));
    }

    [Fact]
    public async Task ListsEachBattleArenaOnceAndSkipsUnusedDottedStageOwners()
    {
        await using PreviewFixture fixture = new();
        fixture.Write("mods/textures/battle/A0STG030.X-0_00.dds", []);
        fixture.Write("mods/textures/eng/battle/A0STG030.X-0_01.png", []);
        fixture.Write("mods/textures/battle/.A0STG030.X-0_02.dds", []);
        fixture.Write("mods/textures/battle/.A0STG031.X-0_00.dds", []);
        fixture.Write("mods/textures/battle/A0STG030.X-0_f0_4_0_243.dds", []);
        fixture.Write("mods/textures/battle/C0M001.DAT-0_00.dds", []);

        ModPreviewDocument document = await fixture.Open();

        ModPreviewEntry arena = Assert.Single(document.Entries, entry => entry.Category == "Battle arenas");
        Assert.Equal("A0STG030", arena.Name);
        Assert.Equal(arena, document.Tree.InitialPath[^1].Entry);
        ModPreviewEntry model = Assert.Single(document.Entries, entry => entry != arena);
        Assert.Equal("C0M001.DAT-0_00", model.Name);
    }

    [Fact]
    public async Task ComparesTheInstalledBattleArenaUsingTheSelectedPackageTextures()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional installed game resources.");

        await using PreviewFixture fixture = new();
        PixelImage texture = new(1, 1);
        texture[0, 0] = new(255, 0, 255);
        fixture.Write("mods/textures/battle/A0STG030.X-0_00.png", LocalizationArtworkExporter.EncodePng(texture));
        fixture.Write("mods/textures/battle/.A0STG030.X-0_01.dds", []);
        ModPreviewDocument document = await fixture.Open();
        ModPreviewEntry arena = Assert.Single(document.Entries);
        ModPreviewResourcesProfile profile = new(game!)
        {
            RenderBattleStage = stage =>
            {
                BattleRenderer renderer = new(320, 240);
                byte[] pixels = renderer.Render(stage, [], BattleCamera.FromNative(stage.IntroductionCameras[0]));
                return new(320, 240, MemoryMarshal.Cast<byte, Rgba32>(pixels).ToArray());
            }
        };

        ModPreviewComparison result = await document.CompareAsync(arena, profile, TestContext.Current.CancellationToken);

        PixelImage before = Assert.IsType<PixelImage>(result.Before.Image);
        PixelImage after = Assert.IsType<PixelImage>(result.After.Image);
        Assert.True(before.Pixels.Distinct().Count() > 2);
        Assert.False(before.Pixels.SequenceEqual(after.Pixels));
    }

    [Fact]
    public async Task ComparesEachSelectedCardWithItsOwnInstalledOriginalAndPackageCrop()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional installed game resources.");

        await using PreviewFixture fixture = new();
        PixelImage sheet = new(1792, 256);
        Rgba32 firstColor = new(10, 20, 30);
        Rgba32 lastColor = new(80, 90, 100);
        sheet[0, 0] = firstColor;
        sheet[13 * 128 + 64, 2 * 64] = lastColor;
        fixture.Write(Cards, LocalizationArtworkExporter.EncodePng(sheet));
        ModPreviewDocument document = await fixture.Open();
        ModPreviewEntry first = Assert.Single(document.Entries, entry => entry.Name == "Card 001");
        ModPreviewEntry last = Assert.Single(document.Entries, entry => entry.Name == "Card 110");

        ModPreviewComparison firstComparison = await document.CompareAsync(first, new(game!), TestContext.Current.CancellationToken);
        ModPreviewComparison lastComparison = await document.CompareAsync(last, new(game!), TestContext.Current.CancellationToken);

        PixelImage firstOriginal = Assert.IsType<PixelImage>(firstComparison.Before.Image);
        PixelImage lastOriginal = Assert.IsType<PixelImage>(lastComparison.Before.Image);
        Assert.Equal(64, firstOriginal.Width);
        Assert.Equal(64, lastOriginal.Width);
        Assert.False(firstOriginal.Pixels.SequenceEqual(lastOriginal.Pixels));
        Assert.Equal(firstColor, Assert.IsType<PixelImage>(firstComparison.After.Image)[0, 0]);
        Assert.Equal(lastColor, Assert.IsType<PixelImage>(lastComparison.After.Image)[0, 0]);
    }

    private sealed class PreviewFixture : IAsyncDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"mod-preview-atlas-{Guid.NewGuid():N}");
        private readonly ModRuntime mods;
        private readonly ModLibraryService library;

        public PreviewFixture()
        {
            Directory.CreateDirectory(PackagePath);
            File.WriteAllText(Path.Combine(PackagePath, "mod.xml"), "<ModInfo><Name>Card artwork</Name></ModInfo>");
            mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
        }

        private string PackagePath => Path.Combine(root, "package");

        public void Write(string relativePath, byte[] content)
        {
            string path = Path.Combine(PackagePath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }

        public Task<ModPreviewDocument> Open()
        {
            ModPackage package = new("Junction/fixture", "Card artwork", ModFormat.Junction, false, false, ModEffectiveStatus.Disabled, PackagePath);
            return library.OpenPreviewAsync(null, package, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await library.StopAsync();
            library.Dispose();
            mods.Dispose();
            Directory.Delete(root, true);
        }
    }
}
