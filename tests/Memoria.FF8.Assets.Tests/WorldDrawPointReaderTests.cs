using System.Buffers.Binary;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldDrawPointReaderTests
{
    [Fact]
    public void OrderedSelectorsCompareSignedMapAndPolygonAndReturnGlobalRecordIndex()
    {
        byte[] section = Section();
        Assert.Equal(2, WorldDrawPointReader.Find(section, [1, 0, 255, 255], 300, 17));
        Assert.Equal(0, WorldDrawPointReader.Find(section, [0, 1, 255, 255], 300, 17));
        Assert.Equal(1, WorldDrawPointReader.Find(section, [0, 255, 255, 255], -2, 255));
        Assert.Equal(-1, WorldDrawPointReader.Find(section, [0, 255, 1, 1], 300, 18));
        Assert.Equal(-1, WorldDrawPointReader.Find(section, [255, 0, 1, 1], 300, 17));
        Assert.Equal(-1, WorldDrawPointReader.Find(section, [128, 0, 1, 1], 300, 17));
        Assert.Equal(2, WorldDrawPointReader.Find(section, [2, 2, 2, 1], 300, 17));
        Assert.Equal(-1, WorldDrawPointReader.Find(section, [0, 0, 0, 0], 300, 18));
    }

    [Fact]
    public void InvalidRelativeBoundsAreRejected()
    {
        byte[] section = Section();
        BinaryPrimitives.WriteInt32LittleEndian(section.AsSpan(4), section.Length + 4);
        Assert.Throws<InvalidDataException>(() => WorldDrawPointReader.Find(section, [0, 255, 255, 255], 300, 17));
    }

    private static byte[] Section()
    {
        byte[] section = new byte[64];
        BinaryPrimitives.WriteInt32LittleEndian(section, 48);
        BinaryPrimitives.WriteInt32LittleEndian(section.AsSpan(4), 56);
        BinaryPrimitives.WriteInt32LittleEndian(section.AsSpan(8), 56);
        BinaryPrimitives.WriteInt32LittleEndian(section.AsSpan(12), 64);
        BinaryPrimitives.WriteInt32LittleEndian(section.AsSpan(40), 48);
        BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(48), 300);
        section[50] = 17;
        section[51] = 255;
        BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(52), -2);
        section[54] = 255;
        BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(56), 300);
        section[58] = 17;
        BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(60), 300);
        section[62] = 18;
        return section;
    }
}
