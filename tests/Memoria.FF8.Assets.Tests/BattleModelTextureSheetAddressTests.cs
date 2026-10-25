using System.Reflection;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleModelTextureSheetAddressTests
{
    [Theory]
    [InlineData(640, 0, 0)]
    [InlineData(640, 128, 1)]
    [InlineData(704, 0, 2)]
    [InlineData(704, 128, 3)]
    [InlineData(768, 0, 4)]
    [InlineData(768, 128, 5)]
    public void MonsterSheetUsesSourceAllocationRatherThanTimDirectoryOrder(int x, int y, int row)
    {
        ResourceKey owner = new("battle/c0m123.dat");
        TextureSourceAddress address = Assert.IsType<TextureSourceAddress>(Address(owner, new(x, y, 8, 128, 128), 6));
        Assert.Equal(new TextureSourceAddress(owner, $"row/{row}", 0, 0, 0, 128, 128, 128, 128), address);
    }

    [Theory]
    [InlineData(0, 128, 0)]
    [InlineData(128, 128, 1)]
    [InlineData(256, 96, 0)]
    [InlineData(384, 96, 1)]
    public void BodySheetUsesTheUnallocatedSourceRowAndPreservesPartialMaterialHeight(int y, int height, int row)
    {
        ResourceKey owner = new("battle/d0c000.dat");
        TextureSourceAddress address = Assert.IsType<TextureSourceAddress>(Address(owner, new(640, y, 8, 128, height), 2));
        Assert.Equal(new TextureSourceAddress(owner, $"row/{row}", 0, 0, 0, 128, height, 128, 128), address);
    }

    [Fact]
    public void WeaponStripHasItsOwnCanvasAndDoesNotUseItsAtlasDestinationAsTheSourceRow()
    {
        ResourceKey owner = new("battle/d5w028.dat");
        TextureSourceAddress address = Assert.IsType<TextureSourceAddress>(Address(owner, new(832, 96, 4, 128, 32), 1));
        Assert.Equal(new TextureSourceAddress(owner, "row/0", 0, 0, 0, 128, 32, 128, 32), address);
        Assert.Null(Address(owner, new(832, 96, 4, 128, 32), 2));
    }

    [Theory]
    [InlineData("battle/a0stg000.x", 640, 0, 8, 128, 128)]
    [InlineData("battle/c0m123.dat", 832, 0, 8, 128, 128)]
    [InlineData("battle/c0m123.dat", 640, 256, 8, 128, 128)]
    [InlineData("battle/c0m123.dat", 640, 0, 8, 128, 96)]
    [InlineData("battle/d0c000.dat", 640, 64, 8, 128, 128)]
    [InlineData("battle/d0c000.dat", 640, -128, 8, 128, 128)]
    [InlineData("battle/d0c000.dat", 640, 0, 8, 256, 128)]
    [InlineData("battle/d0c000.dat", 640, 0, 16, 128, 128)]
    [InlineData("battle/d0w000.dat", 640, 0, 8, 128, 128)]
    [InlineData("battle/d0x000.dat", 640, 0, 8, 128, 128)]
    public void UnsupportedSourceRectanglesDoNotPublishGuessedSheetAddresses(string path, int x, int y, int bits, int width, int height)
    {
        Assert.Null(Address(new(path), new(x, y, bits, width, height), 1));
    }

    [Fact]
    public void SelectedOriginalSourcePublishesIndependentSheetAndTimMemberAddresses()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using IResourceSource source = GameDataSourceFactory.Open(directory!);
        ResourceService resources = new(source);
        CaptureResolver capture = new();
        resources.Prepare(capture).Commit();
        using BattleAssets assets = new(directory!, resources);
        BattleModel body = assets.LoadModel("d0c000.dat");
        _ = assets.LoadWeapon(0, 0, body);
        _ = assets.LoadEnemy(61);
        foreach (string path in new[] { "battle/d0c000.dat", "battle/d0w000.dat", "battle/c0m061.dat" })
        {
            TextureBuildContext[] contexts = capture.Contexts.Where(context => context.Key.Owner.Path == path).ToArray();
            Assert.NotEmpty(contexts);
            foreach (TextureBuildContext context in contexts)
            {
                TextureSourceAddress sheet = Assert.IsType<TextureSourceAddress>(context.NativeSheet);
                TextureSourceAddress member = Assert.IsType<TextureSourceAddress>(context.NativeAlias);
                Assert.Equal(context.Key.Subresource, member.Subresource);
                Assert.StartsWith("texture/", member.Subresource);
                Assert.StartsWith("row/", sheet.Subresource);
                Assert.Equal(context.Key.Owner, sheet.Owner);
                Assert.Equal(context.LogicalWidth, sheet.Width);
                Assert.Equal(context.LogicalHeight, sheet.Height);
                Assert.Equal("classic-battle", context.Layout);
            }
        }
    }

    private static TextureSourceAddress? Address(ResourceKey owner, TextureSourceRegion region, int count)
    {
        MethodInfo method = typeof(BattleAssets).GetMethod("BattleModelSheetAddress", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (TextureSourceAddress?)method.Invoke(null, [owner, region, count]);
    }

    private sealed class CaptureResolver : ITextureReplacementResolver
    {
        public List<TextureBuildContext> Contexts { get; } = [];

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            Contexts.Add(context);
            return null;
        }
    }
}
