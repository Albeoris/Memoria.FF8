using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareVramSpanTests
{
    [Theory]
    [InlineData(0, 4, new ushort[] { 0x1111, 0x2222, 0x8001, 0x4444, 0xA5A5 })]
    [InlineData(1, 4, new ushort[] { 0xA5A5, 0x1111, 0x2222, 0x8001, 0xA5A5 })]
    public void EvenAndOddOpaqueStartsPreserveStpAndExclusiveRight(int left, int right, ushort[] expected)
    {
        byte[] image = Image();
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, 0, left, right, 0, 1024, 0, 0, 256, 512);
        Assert.Equal(expected, Enumerable.Range(0, expected.Length).Select(index => Word(image, index * 2)));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, image.AsSpan(256, 5).ToArray());
    }

    [Fact]
    public void OddTransparentFirstAdvancesUvWithoutAdvancingDestinationAndMayWriteBeyondExclusiveRight()
    {
        byte[] image = Image();
        image[256] = 9;
        Put(image, 512 + 18, 0);
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, 0, 1, 4, 0, 1024, 0, 0, 256, 512);
        Assert.Equal(new ushort[] { 0xA5A5, 0x2222, 0x8001, 0x4444, 0x5555, 0xA5A5 }, Enumerable.Range(0, 6).Select(index => Word(image, index * 2)));
    }

    [Fact]
    public void EqualOddSpanSamplesAndCanWriteButEqualEvenSpanDoesNotAccessSources()
    {
        byte[] image = Image();
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, 0, 1, 1, 0, 0, 0, 0, 256, 512);
        Assert.Equal((ushort)0x1111, Word(image, 2));
        byte[] before = image.ToArray();
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, 0, 2, 2, int.MinValue, int.MaxValue, int.MaxValue, int.MinValue, -1, -1);
        Assert.Equal(before, image);
        image[256] = 9;
        Put(image, 530, 0);
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, 0, 3, 3, 0, 0, 0, 0, 256, 512);
        Assert.Equal((ushort)0xA5A5, Word(image, 6));
    }

    [Fact]
    public void PairPrefetchSeesBothAliasedSourceBytesBeforeEitherDestinationStore()
    {
        byte[] image = Image();
        image[0] = 1;
        image[1] = 2;
        image[2] = 3;
        image[3] = 4;
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, 0, 0, 2, 0, 1024, 0, 0, 0, 512);
        Assert.Equal((ushort)0x1111, Word(image, 0));
        Assert.Equal((ushort)0x2222, Word(image, 2));
        byte[] paletteAlias = Image();
        paletteAlias[256] = 1;
        paletteAlias[257] = 2;
        Put(paletteAlias, 4, 0xAAAA);
        Put(paletteAlias, 2, 0xBBBB);
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(paletteAlias, 0, 2, 4, 0, 1024, 0, 0, 256, 0);
        Assert.Equal((ushort)0xBBBB, Word(paletteAlias, 4));
        Assert.Equal((ushort)0xAAAA, Word(paletteAlias, 6));
    }

    [Fact]
    public void LaterPairSamplesCurrentAliasedDestinationInsteadOfACopiedTextureBank()
    {
        byte[] image = Image();
        image[0] = 1;
        image[1] = 2;
        image[2] = 3;
        image[3] = 4;
        Put(image, 512 + 0x22 * 2, 0x7777);
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, 0, 0, 4, 0, 1024, 0, 0, 0, 512);
        Assert.Equal(new ushort[] { 0x1111, 0x2222, 0x7777, 0x7777 }, Enumerable.Range(0, 4).Select(index => Word(image, index * 2)));
    }

    [Fact]
    public void SignedUAndWrappedVArithmeticUseNativeSarAndNoAdditionalUTextureMask()
    {
        byte[] image = Image();
        image[255] = 3;
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, 0, 0, 1, -1, 0, 0, 0, 256, 512);
        Assert.Equal((ushort)0x8001, Word(image, 0));
        byte[] wrapped = Image();
        wrapped[256] = 4;
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(wrapped, 0, 0, 1, 0, 0, 1 << 21, int.MinValue, 256, 512);
        Assert.Equal((ushort)0x4444, Word(wrapped, 0));
        byte[] highU = Image();
        highU[256 + 300] = 3;
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(highU, 0, 0, 1, 300 * 1024, 0, 0, 0, 256, 512);
        Assert.Equal((ushort)0x8001, Word(highU, 0));
    }

    [Fact]
    public void AccessesOutsideActualTexturePaletteOrDestinationOwnerFailWithoutClamping()
    {
        Assert.Throws<NotSupportedException>(() => NativeSoftwareVramSpan.WriteOpaque8Bit463650(Image(), 0, 0, 1, -1024, 0, 0, 0, 0, 512));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareVramSpan.WriteOpaque8Bit463650(Image(), 0, 0, 1, 0, 0, 0, 0, 1024, 512));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareVramSpan.WriteOpaque8Bit463650(Image(), 0, 0, 1, 0, 0, 0, 0, 256, 1023));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareVramSpan.WriteOpaque8Bit463650(Image(), 1024, 0, 1, 0, 0, 0, 0, 256, 512));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareVramSpan.WriteOpaque8Bit463650(Image(), -2, 0, 0, 0, 0, 0, 0, 256, 512));
    }

    [Fact]
    public void IndependentJsonImageRestorePreservesWholeMutableOwnerAndNextAliasedSpan()
    {
        byte[] image = Image();
        NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, 0, 1, 4, 0, 1024, 0, 0, 256, 512);
        Saved snapshot = new(image, 0, 0, 4, 0, 1024, 0, 0, 0, 512);
        Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(snapshot))!;
        Assert.NotSame(snapshot.Image, restored.Image);
        Apply(snapshot);
        Apply(restored);
        Assert.Equal(snapshot.Image, restored.Image);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored));
    }

    private static void Apply(Saved state) => NativeSoftwareVramSpan.WriteOpaque8Bit463650(state.Image, state.Row, state.Left, state.Right, state.U, state.StepU, state.V, state.StepV, state.Texture, state.Palette);
    private static byte[] Image()
    {
        byte[] image = Enumerable.Repeat((byte)0xA5, 1024).ToArray();
        for (int index = 0; index < 5; index++)
        {
            image[256 + index] = (byte)(index + 1);
        }
        Put(image, 512, 0);
        Put(image, 514, 0x1111);
        Put(image, 516, 0x2222);
        Put(image, 518, 0x8001);
        Put(image, 520, 0x4444);
        Put(image, 522, 0x5555);
        return image;
    }
    private static ushort Word(byte[] image, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(offset, 2));
    private static void Put(byte[] image, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), value);
    private sealed record Saved(byte[] Image, int Row, int Left, int Right, int U, int StepU, int V, int StepV, int Texture, int Palette);
}
