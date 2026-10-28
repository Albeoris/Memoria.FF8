using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;
using Memoria.FF8.Assets.Remastered;

namespace Memoria.FF8.Assets.Tests;

public sealed class RemasteredBattleImageTableTests
{
    private const int Table = 0x5D8;
    private const uint Address = 0x117733D8;

    [Fact]
    public void AllOriginalFieldsAndRepeatedOwnerMembersSurviveParsing()
    {
        byte[] bytes = Image();
        WriteRecord(bytes, 0, "mag321", 321, 0, 2, 832, 256, 64, 128, 2);
        WriteRecord(bytes, 1, "mag321", 321, 1, 1, 768, 256, 64, 128, 1);
        using PEReader image = new(new MemoryStream(bytes));

        var records = RemasteredBattleImageTableReader.Read(image);

        Assert.Equal(121, records.Length);
        Assert.Equal(new("mag321", 321, 0, 2, 832, 256, 64, 128, 2), records[0]);
        Assert.Equal(new("mag321", 321, 1, 1, 768, 256, 64, 128, 1), records[1]);
        Assert.Equal(1120, records[^1].Identity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(120)]
    public void AnEarlyTerminatorIsRejected(int index)
    {
        byte[] bytes = Image();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(Table + index * 32), 0);
        AssertInvalid(bytes);
    }

    [Fact]
    public void MissingFinalTerminatorIsRejected()
    {
        byte[] bytes = Image();
        WriteRecord(bytes, 121, "mag999", 999, 0, 2, 0, 0, 0, 0, 0);
        AssertInvalid(bytes);
    }

    [Fact]
    public void AnUnrelatedMachineProfileIsRejected()
    {
        byte[] bytes = Image();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84), (ushort)Machine.Amd64);
        AssertInvalid(bytes);
    }

    [Fact]
    public void A64BitAddressProfileIsRejectedBeforePointerArithmetic()
    {
        byte[] bytes = Image();
        bytes.AsSpan(0x178, 40).CopyTo(bytes.AsSpan(0x188, 40));
        bytes.AsSpan(0x178, 16).Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x94), 240);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x98), (ushort)PEMagic.PE32Plus);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0xB0), ulong.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x104), 16);
        AssertInvalid(bytes);
    }

    [Fact]
    public void RepeatedNativeSelectorIsRejectedEvenWhenItsNameAndKindDiffer()
    {
        byte[] bytes = Image();
        WriteRecord(bytes, 1, "mag999", 1000, -1, 2, 0, 0, 0, 0, 0);
        AssertInvalid(bytes);
    }

    [Theory]
    [InlineData(0x0FFFFFFF)]
    [InlineData(0x11776000)]
    [InlineData(0x11775FF0)]
    public void NamesMustFitEntirelyInStoredPeSections(uint pointer)
    {
        byte[] bytes = Image();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(Table), pointer);
        AssertInvalid(bytes);
    }

    [Theory]
    [InlineData(0x3000, 0x3F0)]
    [InlineData(0x3F0, 0x3000)]
    public void TheCompleteRecordTableMustFitVirtualAndStoredSectionBounds(int virtualSize, int rawSize)
    {
        byte[] bytes = Image();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x180), virtualSize);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x188), rawSize);
        AssertInvalid(bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../mag001")]
    [InlineData("mag/001")]
    [InlineData("mag\\001")]
    [InlineData("mag:001")]
    [InlineData("маг001")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void NamesMustBeBoundedNullTerminatedResourceStems(string name)
    {
        byte[] bytes = Image();
        int offset = Table + 0x1000;
        bytes.AsSpan(offset, 32).Fill((byte)'a');
        byte[] encoded = Encoding.UTF8.GetBytes(name);
        encoded.CopyTo(bytes, offset);
        if (encoded.Length < 32)
        {
            bytes[offset + encoded.Length] = 0;
        }
        AssertInvalid(bytes);
    }

    [Fact]
    public void AuthenticatedInstalledTableRetainsEffectSelectorsAlongsideMonsterAliases()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run the optional installed table check.");
        using RemasteredGameDataSource source = new(directory!);

        var records = source.ReadBattleImageTable();

        Assert.Equal(121, records.Length);
        Assert.Contains(new("mag005", 5, 0, 2, 832, 256, 64, 128, 2), records);
        Assert.Contains(new("mag201", 201, 0, 2, 832, 256, 64, 128, 1), records);
        Assert.Contains(new("mag201", 201, 1, 2, 768, 256, 64, 128, 2), records);
        Assert.Contains(records, record => record.Identity == 1055 && record.Member == -1 && record.Name == "mag900");
        Assert.Equal(records, source.ReadBattleImageTable());
    }

    private static void AssertInvalid(byte[] bytes)
    {
        using PEReader image = new(new MemoryStream(bytes));
        Assert.Throws<InvalidDataException>(() => RemasteredBattleImageTableReader.Read(image));
    }

    private static byte[] Image()
    {
        byte[] bytes = new byte[0x3200];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0x5A4D);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3C), 0x80);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x80), 0x4550);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84), 0x14C);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x86), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x94), 224);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x98), 0x10B);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xB4), 0x10000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xB8), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xBC), 0x200);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xD0), 0x1776000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xD4), 0x200);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xF4), 16);
        ".data"u8.CopyTo(bytes.AsSpan(0x178));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x180), 0x3000);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x184), 0x1773000);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x188), 0x3000);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x18C), 0x200);
        for (int index = 0; index < 121; index++)
        {
            WriteRecord(bytes, index, $"c0m{index:000}", 1000 + index, -1, 0, 0, 0, 0, 0, 0);
        }
        return bytes;
    }

    private static void WriteRecord(byte[] bytes, int index, string name, int identity, int member, int kind, ushort x, ushort y, ushort width, ushort height, int mode)
    {
        Span<byte> record = bytes.AsSpan(Table + index * 32, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(record, Address + 0x1000 + (uint)index * 32);
        BinaryPrimitives.WriteInt32LittleEndian(record[8..], identity);
        BinaryPrimitives.WriteInt32LittleEndian(record[12..], member);
        BinaryPrimitives.WriteInt32LittleEndian(record[16..], kind);
        BinaryPrimitives.WriteUInt16LittleEndian(record[20..], x);
        BinaryPrimitives.WriteUInt16LittleEndian(record[22..], y);
        BinaryPrimitives.WriteUInt16LittleEndian(record[24..], width);
        BinaryPrimitives.WriteUInt16LittleEndian(record[26..], height);
        BinaryPrimitives.WriteInt32LittleEndian(record[28..], mode);
        Encoding.ASCII.GetBytes(name).CopyTo(bytes, Table + 0x1000 + index * 32);
    }
}
