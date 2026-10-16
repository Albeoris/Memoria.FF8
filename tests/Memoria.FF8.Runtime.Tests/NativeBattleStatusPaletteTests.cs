using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStatusPaletteTests
{
    [Theory]
    [InlineData(0x0000, 0x94A5)]
    [InlineData(0x001F, 0xAD6B)]
    [InlineData(0x03E0, 0xC631)]
    [InlineData(0x7C00, 0xAD6B)]
    [InlineData(0x7FFF, 0xF7BD)]
    [InlineData(0xFFFF, 0xF7BD)]
    public void PetrifyProjectsTheOriginalWeightedLuminanceAndIgnoresSourceStp(ushort original, ushort expected)
    {
        byte[] palette = Filled(original);
        byte[] result = NativeBattleStatusPalette.Transform509E00(palette, 0x410).ToArray();
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(2)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(result));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(512)));
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(1022)));
        Assert.Equal(original, BinaryPrimitives.ReadUInt16LittleEndian(palette));
    }

    [Theory]
    [InlineData(0x0000, 0x8000)]
    [InlineData(0x03E0, 0xA980)]
    [InlineData(0x7FFF, 0x8320)]
    public void FreezeProjectsGreenAndSineBlueFromSavedColors(ushort original, ushort expected)
    {
        byte[] result = NativeBattleStatusPalette.Transform509E00(Filled(original), 0x400).ToArray();
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(2)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(result));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(512)));
    }

    [Fact]
    public void RemovingBothStatusesRestoresAllOriginalWordsIncludingTransparentEntries()
    {
        byte[] original = Filled(0x8462);
        Assert.Equal(original, NativeBattleStatusPalette.Transform509E00(original, 0x1000).ToArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleStatusPalette.Transform509E00(original.AsSpan(0, 512), 0x10));
    }

    private static byte[] Filled(ushort color)
    {
        byte[] bytes = new byte[1024];
        for (int index = 0; index < 512; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2 * index), color);
        }
        return bytes;
    }
}
