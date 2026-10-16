using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffectTextureReaderTests
{
    [Fact]
    public void OriginalFireTimUploadsAndDecodesItsActualClutAndImage()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeBattleEffectInitializerAsset asset = assets.ReadNativeEffectCatalog()[1].InitializerAsset!;
        NativeBattleEffectHeapAllocation source = new(asset.Name, asset.Data);
        TimFile original = TimFile.Read(asset.Data.AsSpan());
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        int offset = 0;
        foreach (TimImage image in original.Images)
        {
            var queued = state.EnqueueTim505E30(source, offset);
            state = queued.State;
            offset = queued.NextOffset;
        }
        Assert.Equal(asset.Data.Length, offset);
        NativeBattleEffectTextureDrain drain = state.Drain505D20();
        Assert.Equal(2 * original.Images.Count, drain.Uploads.Length);
        foreach (TimImage image in original.Images)
        {
            Assert.NotNull(image.Clut);
            int depth = image.PixelMode == TimPixelMode.Indexed4 ? 0 : 1;
            ushort clut = (ushort)((image.Clut!.Y << 6) | (image.Clut.X / 16));
            int pixelsPerWord = depth == 0 ? 4 : 2;
            PixelImage expected = image.Decode();
            //A TIM can occupy several native pages. Each original sprite still selects one page with byte UVs.
            for (int row = 0; row < image.Height;)
            {
                int v = (image.Y + row) % 256;
                int height = Math.Min(image.Height - row, 256 - v);
                for (int column = 0; column < image.PixelWidth;)
                {
                    int wordX = image.X + column / pixelsPerWord;
                    int u = wordX % 64 * pixelsPerWord;
                    int width = Math.Min(image.PixelWidth - column, 256 - u);
                    ushort page = (ushort)((wordX / 64) | (((image.Y + row) / 256) << 4) | (depth << 7));
                    PixelImage decoded = NativeBattleEffectTextureReader.DecodeRegion(drain.State, page, clut, u, v, width, height);
                    for (int line = 0; line < height; line++)
                    {
                        Assert.Equal(expected.Pixels.AsSpan((row + line) * image.PixelWidth + column, width).ToArray(), decoded.Pixels.AsSpan(line * width, width).ToArray());
                    }
                    column += width;
                }
                row += height;
            }
        }
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void NativePixelDepthSelectsPackedWordsAndPalette(int depth, int width)
    {
        byte[] colors = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(2), 31);
        BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(4), 0x83E0);
        byte[] pixels = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(pixels, depth == 0 ? (ushort)0x0021 : depth == 1 ? (ushort)0x0201 : (ushort)31);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with
        {
            VramRegions = [new(0, colors.ToImmutableArray()), new(2 * 1024, pixels.ToImmutableArray())]
        };
        PixelImage result = NativeBattleEffectTextureReader.DecodeRegion(state, (ushort)(depth << 7), 0, 0, 1, width, 1);
        Assert.Equal(new Rgba32(255, 0, 0), result[0, 0]);
        if (depth < 2)
        {
            Assert.Equal(new Rgba32(0, 255, 0, 128), result[1, 0]);
        }
        if (depth == 0)
        {
            Assert.Equal(default, result[2, 0]);
            Assert.Equal(default, result[3, 0]);
        }
        Assert.Equal(default, NativeBattleEffectTextureReader.DecodeRegion(NativeBattleEffectTextureState.FromColdProcess(), 0, 0, 0, 0, 1, 1)[0, 0]);
    }

    [Fact]
    public void OriginalFireSpriteResourcesUseTheirOwnDynamicFrameDirectories()
    {
        byte[] executable = File.ReadAllBytes(@"C:\Steam\steamapps\common\FINAL FANTASY VIII\FF8_EN.exe");
        foreach (uint address in new uint[] { 0xDEDC2C, 0xDEDDD8, 0xDEDF84 })
        {
            NativeBattleModuleResource resource = NativeBattleEffectStaticResourcesReader.ReadSprite(executable, address);
            resource.Validate();
            Assert.Equal(address, resource.Address);
            Assert.Equal(0, resource.Kind);
            Assert.True(resource.Bytes.Length > 12);
        }
        ImmutableArray<byte> descriptor = NativeBattleEffectStaticResourcesReader.Read(executable, 0xDED5FC, 0x40);
        Assert.Equal(0x40, descriptor.Length);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectStaticResourcesReader.ReadSprite(new byte[100], 0xDEDC2C));
    }
}
