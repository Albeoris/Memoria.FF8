using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldTextureAnimationTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(29, 0)]
    [InlineData(30, 1)]
    [InlineData(62, 2)]
    [InlineData(94, 3)]
    [InlineData(126, 2)]
    [InlineData(158, 1)]
    [InlineData(190, 0)]
    [InlineData(383, 0)]
    public void ReflectedTextureSequenceUsesNativePhaseDelayAndSixStepPeriod(int clock, int expected)
    {
        WorldTextureAnimationSequence sequence = new(2, 32, 4, true, 832, 384, []);
        Assert.Equal(expected, sequence.SelectFrame(clock));
    }

    [Fact]
    public void PaletteReflectionRetainsItsDistinctEndDwellAndRelativeFrameTable()
    {
        WorldTextureAnimationSequence palette = new(0, 1, 3, true, 0, 0, []);
        Assert.Equal(new[] { 0, 1, 2, 1, 0, 0 }, Enumerable.Range(0, 6).Select(clock => palette.SelectFrame(clock, palette: true)));
        byte[] bytes = new byte[80];
        bytes[0] = 3;
        bytes[1] = 8;
        bytes[2] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), 768);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), 193);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 32);
        WorldTextureAnimationSequence parsed = WorldTextureAnimationSequence.Read(bytes, 0, palette: true);
        Assert.Equal(new[] { 20, 44 }, parsed.FrameOffsets);
        Assert.Equal((768, 193), (parsed.DestinationX, parsed.DestinationY));
        Assert.Equal(1, parsed.SelectFrame(5, palette: true));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), int.MaxValue);
        Assert.Throws<OverflowException>(() => WorldTextureAnimationSequence.Read(bytes, 0, palette: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void EveryWaterSelectorUsesNativeVramLayoutAndSelectedClutAcrossAnimation(int selector)
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        byte[] wmset = resources.Read(NativeResourceNames.WorldSettings(resources.ResourceLanguage));
        using WorldAssets assets = new(TestResources.GameDirectory, resources);
        WorldTriangle triangle = Water(selector);
        int[] members = [20, 16, 21, 22, 17, 18, 19, 23];
        for (int clock = 0; clock < 384; clock += 2)
        {
            PixelImage atlas = assets.GetTexture(triangle, animation: new(clock, clock));
            Assert.Equal((256, 128), (atlas.Width, atlas.Height));
            foreach (int tile in new[] { 0, 2, 3, 4, 5, 6, 7 })
            {
                int section = I32(wmset, 37 * 4);
                int member = members[tile];
                int texture = section + I32(wmset, section + member * 4);
                int paletteMember = members[selector];
                int paletteTexture = section + I32(wmset, section + paletteMember * 4);
                int palette = paletteTexture + 20;
                int paletteSection = I32(wmset, 40 * 4);
                int paletteRecord = selector switch { 4 => 0, 5 => 1, 6 => 2, 1 => 3, _ => -1 };
                if (paletteRecord >= 0)
                {
                    int record = paletteSection + I32(wmset, paletteSection + paletteRecord * 4);
                    int frame = (clock + wmset[record]) / wmset[record + 1] % wmset[record + 2];
                    int table = record + 12;
                    palette = table + I32(wmset, table + frame * 4) + 20;
                }
                foreach ((int x, int y) in new[] { (7, 9), (31, 31), (49, 51) })
                {
                    int pixelOffset = texture + 8 + I32(wmset, texture + 8) + 12 + y * 64 + x;
                    if (tile is 2 or 3)
                    {
                        int animationSection = I32(wmset, 16 * 4);
                        int animationRecord = tile == 2 ? 0 : y < 32 ? 1 : 2;
                        int record = animationSection + I32(wmset, animationSection + animationRecord * 4);
                        int frame = (clock + 2) / 32 % (animationRecord < 2 ? 6 : 4);
                        if (frame >= 4)
                        {
                            frame = 6 - frame;
                        }
                        int table = record + 8;
                        pixelOffset = table + I32(wmset, table + frame * 4) + 20 + (tile == 3 ? y % 32 : y) * 64 + x;
                    }
                    byte colorIndex = wmset[pixelOffset];
                    Rgba32 expected = Rgba32.FromPsxBgr555(BinaryPrimitives.ReadUInt16LittleEndian(wmset.AsSpan(palette + colorIndex * 2)));
                    Assert.Equal(expected, atlas[tile % 4 * 64 + x, tile / 4 * 64 + y]);
                }
            }
        }
        Assert.Same(assets.GetTexture(triangle, animation: new(0, 0)), assets.GetTexture(triangle, animation: new(0, 48)));
    }

    [Fact]
    public void CachedAnimationSourcesFollowReplacementPublicationAndRelease()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        using (WorldAssets assets = new(TestResources.GameDirectory, resources))
        {
            WorldTriangle triangle = Water(2);
            _ = assets.GetTexture(triangle, animation: new(0, 0));
            _ = assets.GetTexture(triangle, animation: new(32, 32));
            resources.Prepare(new WaterResolver(30)).Commit();
            Assert.Equal(new Rgba32(30, 0, 0), assets.GetTexture(triangle, animation: new(0, 0))[260, 20]);
            Assert.Equal(new Rgba32(30, 0, 0), assets.GetTexture(triangle, animation: new(32, 32))[260, 20]);
            resources.Prepare(new WaterResolver(60)).Commit();
            Assert.Equal(new Rgba32(60, 0, 0), assets.GetTexture(triangle, animation: new(32, 32))[260, 20]);
        }
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    private static WorldTriangle Water(int selector) => new(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, Vector2.Zero, Vector2.Zero, Vector2.Zero, 0, (byte)selector, 0, 0x40, 0);
    private static int I32(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

    private sealed class WaterResolver(byte red) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            return context.NativeAlias is { Subresource: "section/38/texture/21" } ? new(new PixelImage(128, 128, Enumerable.Repeat(new Rgba32(red, 0, 0), 128 * 128).ToArray()), "replacement", 64, 64) : null;
        }
    }
}
