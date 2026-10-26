using System.Buffers.Binary;
using System.Text;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldArchiveSnapshotTests
{
    [Fact]
    public void ListingDiscoverySharesCanonicalOwnershipWithoutRequiringPayloads()
    {
        var fixture = Create(("labels/ROOM.JSM", [1], 0, 1), ("elsewhere/chara.one", [2], 0, 1));
        IReadOnlyList<string> names = FieldArchiveSnapshot.ReadMemberNames("room", fixture.Listing);
        Assert.Equal(new[] { "room.jsm", "chara.one" }, names);
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, [], []));
        Assert.Equal(names, FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content).Select(member => member.Name));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)names)[0] = "foreign.jsm");
        Array.Clear(fixture.Listing);
        Assert.Equal("room.jsm", names[0]);
    }

    [Fact]
    public void RawMembersKeepCanonicalOwnershipAndDormantMetadata()
    {
        string[] extensions = ["jsm", "msd", "sym", "one", "ca", "id", "inf", "map", "mim", "mrt", "pmd", "pmp", "pvp", "rat", "tdw"];
        var fixture = Create(extensions.Select((extension, number) => ($"labels/ROOM.{extension.ToUpperInvariant()}", new byte[] { (byte)number }, 0, 1u)).Append(("another/chara.one", new byte[] { 99 }, 0, 1u)).ToArray());
        byte[] paddedIndex = new byte[fixture.Index.Length + 17];
        fixture.Index.CopyTo(paddedIndex, 0);
        IReadOnlyList<FieldArchiveMember> result = FieldArchiveSnapshot.Read("room", fixture.Listing, paddedIndex, fixture.Content);
        Assert.Equal(16, result.Count);
        Assert.Equal(extensions.Select(extension => $"room.{extension}").Append("chara.one"), result.Select(member => member.Name));
        Assert.Equal(new byte[] { 99 }, result[^1].Data);
        fixture.Content[0] = 255;
        Assert.Equal(new byte[] { 0 }, result[0].Data);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CompressedMembersUseSharedArchiveDecoder(int compression)
    {
        byte[] payload = compression == 1 ? [7, 65, 66, 67] : [0x30, 65, 66, 67];
        var fixture = Create(("room.jsm", Frame(compression, payload, 3), compression, 3));
        Assert.Equal("ABC"u8.ToArray(), Assert.Single(FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content)).Data);
    }

    [Fact]
    public void EmptyNativeEntriesIgnoreSentinelOffsets()
    {
        var fixture = Create(("room.sym", [], 0, 0));
        BinaryPrimitives.WriteUInt32LittleEndian(fixture.Index.AsSpan(4), uint.MaxValue);
        Assert.Empty(Assert.Single(FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content)).Data);
    }

    [Theory]
    [InlineData("../room.jsm")]
    [InlineData("labels/../room.jsm")]
    [InlineData("labels//room.jsm")]
    [InlineData("foreign.jsm")]
    [InlineData("room.jsm/foreign.msd")]
    [InlineData("room.jsm:stream")]
    [InlineData("room.")]
    [InlineData("room.jsm.more")]
    public void UnsafeOrForeignOwnershipIsRejected(string name)
    {
        var fixture = Create((name, [1], 0, 1));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.ReadMemberNames("room", fixture.Listing));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content));
    }

    [Fact]
    public void CanonicalDuplicatesCannotHideBehindDifferentLabels()
    {
        var fixture = Create(("first/room.jsm", [1], 0, 1), ("second/ROOM.JSM", [2], 0, 1));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.ReadMemberNames("room", fixture.Listing));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content));
    }

    [Fact]
    public void ListingAndMetricsMustAgree()
    {
        var fixture = Create(("room.jsm", [1], 0, 1));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index[..11], fixture.Content));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, [.. fixture.Index, 1], fixture.Content));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", "room.jsm\n"u8.ToArray(), fixture.Index, fixture.Content));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", [.. fixture.Listing, 255], fixture.Index, fixture.Content));
    }

    [Fact]
    public void RawBoundsAndCompressionKindAreValidated()
    {
        var fixture = Create(("room.jsm", [1], 0, 2));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content));
        BinaryPrimitives.WriteUInt32LittleEndian(fixture.Index.AsSpan(4), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content));
        fixture = Create(("room.jsm", [1], 3, 1));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content));
    }

    [Fact]
    public void FramesCannotCrossAnotherNativeAllocation()
    {
        var fixture = Create(("room.jsm", Frame(1, [1, 65], 1), 1, 1), ("room.msd", [2], 0, 1));
        BinaryPrimitives.WriteUInt32LittleEndian(fixture.Index.AsSpan(16), 2);
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TruncatedCompressedInputAndDecodedOutputAreRejected(int compression)
    {
        byte[] frame = Frame(compression, compression == 1 ? [7, 65, 66] : [0x30, 65, 66], 3);
        var fixture = Create(("room.jsm", frame, compression, 3));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content[..^1]));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, [1, 2, 3]));
    }

    [Theory]
    [InlineData(0, 7u)]
    [InlineData(4, 0u)]
    [InlineData(8, 9u)]
    public void Lz4FrameSizeMagicAndOutputSizeMustAgree(int position, uint value)
    {
        byte[] frame = Frame(2, [0x10, 65], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(position), value);
        var fixture = Create(("room.jsm", frame, 2, 1));
        Assert.Throws<InvalidDataException>(() => FieldArchiveSnapshot.Read("room", fixture.Listing, fixture.Index, fixture.Content));
    }

    private static byte[] Frame(int compression, byte[] payload, uint size)
    {
        int header = compression == 1 ? 4 : 12;
        byte[] frame = new byte[header + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)payload.Length + (compression == 1 ? 0u : 8u));
        if (compression == 2)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), 0x5F4C5A34);
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), size);
        }
        payload.CopyTo(frame, header);
        return frame;
    }

    private static (byte[] Listing, byte[] Index, byte[] Content) Create(params (string Name, byte[] Stored, int Compression, uint Size)[] members)
    {
        byte[] listing = Encoding.ASCII.GetBytes(string.Join("\r\n", members.Select(member => "c:\\ff8\\data\\" + member.Name)) + "\r\n");
        byte[] index = new byte[members.Length * 12];
        using MemoryStream content = new();
        for (int number = 0; number < members.Length; number++)
        {
            var member = members[number];
            Span<byte> record = index.AsSpan(number * 12, 12);
            BinaryPrimitives.WriteUInt32LittleEndian(record, member.Size);
            BinaryPrimitives.WriteUInt32LittleEndian(record[4..], (uint)content.Length);
            BinaryPrimitives.WriteInt32LittleEndian(record[8..], member.Compression);
            content.Write(member.Stored);
        }
        return (listing, index, content.ToArray());
    }
}
