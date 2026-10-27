using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDatDirectoryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(-1)]
    public void UsesActualLoadedExtentInsteadOfTerminalSizeMetadata(int declaredLength)
    {
        byte[] bytes = Directory(16, 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), declaredLength);
        byte[] original = bytes.ToArray();

        Assert.Equal(16, NativeBattleDatDirectory.Section(bytes, 0));
        Assert.Equal(24, NativeBattleDatDirectory.Section(bytes, 1));
        Assert.Equal(32, NativeBattleDatDirectory.Section(bytes, 2));
        Assert.Equal(original, bytes);
    }

    [Theory]
    [InlineData(15, 24)]
    [InlineData(24, 16)]
    [InlineData(16, 33)]
    [InlineData(-1, 24)]
    public void RejectsOffsetsOutsideActualOrderedSectionBounds(int first, int second)
    {
        byte[] bytes = Directory(first, second);

        Assert.Throws<InvalidDataException>(() => NativeBattleDatDirectory.Section(bytes, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleDatDirectory.Section(bytes, 2));
    }

    [Fact]
    public void AllowsEmptySectionsAtActualEnd()
    {
        byte[] bytes = Directory(32, 32);

        Assert.Equal(32, NativeBattleDatDirectory.Section(bytes, 0));
        Assert.Equal(32, NativeBattleDatDirectory.Section(bytes, 1));
        Assert.Equal(32, NativeBattleDatDirectory.Section(bytes, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void PreservesSupportedCountBounds(int count)
    {
        byte[] bytes = Directory(16, 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);

        Assert.Throws<InvalidDataException>(() => NativeBattleDatDirectory.Section(bytes, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void RejectsInvalidSectionSelections(int index)
    {
        byte[] bytes = Directory(16, 24);

        Assert.Throws<InvalidDataException>(() => NativeBattleDatDirectory.Section(bytes, index));
    }

    [Fact]
    public void RequiresCompleteDirectoryIncludingTerminalMetadata()
    {
        byte[] bytes = Directory(16, 24)[..15];

        Assert.Throws<InvalidDataException>(() => NativeBattleDatDirectory.Section(bytes, 0));
    }

    private static byte[] Directory(int first, int second)
    {
        byte[] bytes = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), first);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), second);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), bytes.Length);
        return bytes;
    }
}
