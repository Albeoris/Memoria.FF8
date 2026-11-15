using System.Buffers.Binary;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7CharacterNamingResourcesTests
{
    [Fact]
    public void InstructionOperandsSelectResetsSkipsTemplatesAndSaveRecordMap()
    {
        Dictionary<uint, byte[]> image = Image();
        Ff7CharacterNamingResources resources = Read(image);
        Assert.Equal(new byte[] { 2, 8 }, resources.ResetIdentities);
        Assert.Equal(new byte[] { 3, 10 }, resources.SkipJoinIdentities);
        Assert.True(resources.ResetsRecord(2));
        Assert.False(resources.ResetsRecord(6));
        Assert.True(resources.SkipsJoin(3));
        Assert.False(resources.SkipsJoin(0));
        Assert.Equal(1, resources.RecordId(9));
        Assert.Equal(8, resources.RecordId(10));
        Assert.Equal(132, resources.RecordSize);
        byte[] template = resources.CreateTemplate(8);
        Assert.Equal(8, template[0]);
        Assert.Equal(9, template[1]);
        Assert.Equal(0xA8, template[131]);
        template[0] = 0;
        image[0x200000][8 * 132 + 1] = 50;
        Assert.Equal(8, resources.CreateTemplate(8)[0]);
        Assert.Equal(9, resources.CreateTemplate(8)[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => resources.CreateTemplate(11));
        Assert.Throws<ArgumentOutOfRangeException>(() => resources.RecordId(255));
    }

    [Fact]
    public void ChangedControlFlowStridePointersAndSelectorBindingsAreRejected()
    {
        foreach ((uint address, int offset, byte value) in new (uint, int, byte)[]
        {
            (0x718A7A, 0, 0x90),
            (0x718A7A, 8, 9),
            (0x718A7A, 10, 6),
            (0x718A7A, 12, 0),
            (0x718A7A, 21, 1),
            (0x718ADA, 18, 128),
            (0x718ADA, 32, 128),
            (0x718ADA, 43, 32),
            (0x718ADA, 48, 0xA4),
            (0x719B81, 11, 0),
            (0x719B81, 20, 0),
            (0x719B81, 22, 1),
            (0x719B81, 35, 128),
            (0x719B81, 41, 1),
            (0x719B81, 47, 0)
        })
        {
            Dictionary<uint, byte[]> image = Image();
            image[address][offset] = value;
            Assert.Throws<InvalidDataException>(() => Read(image));
        }
    }

    [Fact]
    public void InvalidTablesRecordsAndTruncatedMappedStorageAreRejected()
    {
        Dictionary<uint, byte[]> image = Image();
        image[0x100000][0] = 9;
        Assert.Throws<InvalidDataException>(() => Read(image));
        image = Image();
        image[0x100000][4] = 0;
        Assert.Throws<InvalidDataException>(() => Read(image));
        image = Image();
        image[0x200000][2 * 132] = 6;
        Assert.Throws<InvalidDataException>(() => Read(image));
        image = Image();
        image[0x200000][2 * 132 + 1] = 0;
        Assert.Throws<InvalidDataException>(() => Read(image));
        image = Image();
        image[0x200000] = image[0x200000][..^1];
        Assert.Throws<InvalidDataException>(() => Read(image));
        image = Image();
        image[0x718A7A][6] = 11;
        image[0x718A7A][10] = 11;
        Assert.Throws<InvalidDataException>(() => Read(image));
        image = Image();
        image[0x719B81][9] = 11;
        Assert.Throws<InvalidDataException>(() => Read(image));
    }

    [Fact]
    public void OriginalNamingSelectorsAndCompleteReusedRecordsComeFromRecognizedImage()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the naming profile check.");
        Ff7CharacterNamingResources resources = Ff7CharacterNamingResources.Load(Ff7Installation.Discover(root));
        Assert.Equal(new byte[] { 6, 7 }, resources.ResetIdentities);
        Assert.Equal(new byte[] { 9, 0 }, resources.SkipJoinIdentities);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 6, 7 }, resources.SaveRecordIds);
        CheckTemplate(6, 224, 18, 14);
        CheckTemplate(7, 178, 18, 16);

        void CheckTemplate(byte identity, ushort hp, ushort mp, uint experience)
        {
            byte[] record = resources.CreateTemplate(identity);
            Assert.Equal(132, record.Length);
            Assert.Equal(identity, record[0]);
            Assert.Equal(1, record[1]);
            Assert.Equal(hp, BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(44)));
            Assert.Equal(hp, BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(46)));
            Assert.Equal(mp, BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(48)));
            Assert.Equal(mp, BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(50)));
            Assert.Equal(experience, BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(60)));
        }
    }

    private static Ff7CharacterNamingResources Read(Dictionary<uint, byte[]> image)
    {
        return Ff7CharacterNamingResources.Read((address, count) => image.TryGetValue(address, out byte[]? data) ? data : throw new InvalidDataException("Unmapped synthetic resource."));
    }

    private static Dictionary<uint, byte[]> Image()
    {
        byte[] initialization = new byte[38];
        for (int index = 0; index < 2; index++)
        {
            int offset = index * 19;
            byte identity = index == 0 ? (byte)2 : (byte)8;
            Put(initialization, offset, [0x83, 0x3D]);
            Word(initialization, offset + 2, 0x300000);
            Put(initialization, offset + 6, [identity, 0x75, 10, 0x6A, identity, 0xE8]);
            Word(initialization, offset + 12, unchecked(0x718ADAu - (0x718A7Au + (uint)offset + 16)));
            Put(initialization, offset + 16, [0x83, 0xC4, 4]);
        }
        byte[] copy = new byte[55];
        Put(copy, 0, [0x55, 0x8B, 0xEC, 0x51, 0x56, 0x57, 0xC7, 0x45, 0xFC]);
        Word(copy, 9, 0x200000);
        Put(copy, 13, [0x8B, 0x45, 8, 0x69, 0xC0]);
        Word(copy, 18, 132);
        Put(copy, 22, [0x8B, 0x75, 0xFC, 3, 0xF0, 0x8B, 0x7D, 8, 0x69, 0xFF]);
        Word(copy, 32, 132);
        Put(copy, 36, [0x81, 0xC7]);
        Word(copy, 38, 0x400000);
        copy[42] = 0xB9;
        Word(copy, 43, 33);
        Put(copy, 47, [0xF3, 0xA5, 0x5F, 0x5E, 0x8B, 0xE5, 0x5D, 0xC3]);
        byte[] completion = new byte[54];
        Put(completion, 0, [0x55, 0x8B, 0xEC, 0x83, 0x3D]);
        Word(completion, 5, 0x300000);
        Put(completion, 9, [3, 0x74, 116, 0x83, 0x3D]);
        Word(completion, 14, 0x300000);
        Put(completion, 18, [10, 0x74, 107, 0xA1]);
        Word(completion, 22, 0x300000);
        Put(completion, 26, [0x8B, 0x0C, 0x85]);
        Word(completion, 29, 0x100000);
        Put(completion, 33, [0x69, 0xC9]);
        Word(completion, 35, 132);
        Put(completion, 39, [0x81, 0xC1]);
        Word(completion, 41, 0x400000);
        Put(completion, 45, [0x51, 0xE8]);
        Word(completion, 47, unchecked(0x5C7336u - (0x719B81u + 51)));
        Put(completion, 51, [0x83, 0xC4, 4]);
        byte[] map = new byte[44];
        byte[] templates = new byte[11 * 132];
        for (int identity = 0; identity < 11; identity++)
        {
            Word(map, identity * 4, (uint)(identity < 9 ? identity : identity == 9 ? 1 : 8));
            templates[identity * 132] = (byte)identity;
            templates[identity * 132 + 1] = (byte)(identity + 1);
            templates[identity * 132 + 131] = (byte)(0xA0 + identity);
        }
        return new() { [0x718A7A] = initialization, [0x718ADA] = copy, [0x719B81] = completion, [0x100000] = map, [0x200000] = templates };
    }

    private static void Put(byte[] destination, int offset, ReadOnlySpan<byte> data)
    {
        data.CopyTo(destination.AsSpan(offset));
    }

    private static void Word(byte[] destination, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset), value);
    }
}
