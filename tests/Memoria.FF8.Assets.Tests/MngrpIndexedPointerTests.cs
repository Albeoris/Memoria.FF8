using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Memoria.FF8.Assets.Tests;

public sealed class MngrpIndexedPointerTests
{
    [Fact]
    public void ReorderedSharedAndSuffixPointersRetainLogicalTextAndOriginalStorage()
    {
        byte[] body = new byte[2048];
        Word(body, 0, 5);
        int[] offsets = [18, 12, 18, 14, 0];
        for (int index = 0; index < offsets.Length; index++)
        {
            Word(body, 2 + index * 2, offsets[index]);
        }
        "hello\0world\0"u8.CopyTo(body.AsSpan(12));
        MngrpFilePair files = Files(87, body);
        MngrpArchive archive = MngrpArchive.Read(files, Encoding.UTF8);
        MngrpStringTable table = archive.GetSection<MngrpStringTableSection>(87).Table;
        Assert.Equal(new string?[] { "world", "hello", "world", "llo", null }, table.Entries.Select(entry => entry.Text?.Value));
        Assert.Equal(files.Content, archive.Write().Content);
        Assert.Equal(files.Header, archive.Write().Header);
        table[0] = "planet";
        MngrpStringTable changed = MngrpArchive.Read(archive.Write(), Encoding.UTF8).GetSection<MngrpStringTableSection>(87).Table;
        Assert.Equal(new string?[] { "planet", "hello", "world", "llo", null }, changed.Entries.Select(entry => entry.Text?.Value));
    }

    [Fact]
    public void CrossTableHeaderTextUsesParentBoundsAndMaterializesWhenEdited()
    {
        byte[] body = new byte[2048];
        Word(body, 0, 2);
        Word(body, 2, 6);
        Word(body, 4, 14);
        Word(body, 6, 2);
        Word(body, 8, 6);
        Word(body, 10, 8);
        body[12] = (byte)'a';
        Word(body, 14, 34);
        MngrpFilePair files = Files(2, body);
        MngrpArchive archive = MngrpArchive.Read(files, Encoding.UTF8);
        MngrpStringTableGroupSection group = archive.GetSection<MngrpStringTableGroupSection>(2);
        Assert.Equal("a", group.Tables[0]![0]);
        Assert.Equal("\"", group.Tables[0]![1]);
        Assert.Equal(34, group.Tables[1]!.Entries.Count);
        Assert.Equal(files.Content, archive.Write().Content);
        Assert.Equal(files.Header, archive.Write().Header);
        group.Tables[0]![0] = "a much longer label";
        MngrpStringTableGroupSection changed = MngrpArchive.Read(archive.Write(), Encoding.UTF8).GetSection<MngrpStringTableGroupSection>(2);
        Assert.Equal("a much longer label", changed.Tables[0]![0]);
        Assert.Equal("\"", changed.Tables[0]![1]);
        Assert.Equal(34, changed.Tables[1]!.Entries.Count);
    }

    [Fact]
    public void ReorderedAndSharedNestedTablePointersKeepLogicalGroups()
    {
        byte[] body = new byte[2048];
        Word(body, 0, 3);
        Word(body, 2, 16);
        Word(body, 4, 8);
        Word(body, 6, 16);
        Word(body, 8, 1);
        Word(body, 10, 4);
        "one\0"u8.CopyTo(body.AsSpan(12));
        Word(body, 16, 1);
        Word(body, 18, 4);
        "two\0"u8.CopyTo(body.AsSpan(20));
        MngrpFilePair files = Files(2, body);
        MngrpArchive archive = MngrpArchive.Read(files, Encoding.UTF8);
        MngrpStringTableGroupSection group = archive.GetSection<MngrpStringTableGroupSection>(2);
        Assert.Equal(new[] { "two", "one", "two" }, group.Tables.Select(table => table![0]));
        Assert.Equal(files.Content, archive.Write().Content);
        group.Tables[0]![0] = "changed";
        var changed = MngrpArchive.Read(archive.Write(), Encoding.UTF8).GetSection<MngrpStringTableGroupSection>(2);
        Assert.Equal(new[] { "changed", "one", "two" }, changed.Tables.Select(table => table![0]));
    }

    [Theory]
    [InlineData("header")]
    [InlineData("parent")]
    [InlineData("unterminated")]
    public void InvalidParentPointersAndMissingTerminatorsAreRejected(string defect)
    {
        byte[] body = new byte[2048];
        Word(body, 0, 1);
        Word(body, 2, defect == "header" ? 2 : defect == "parent" ? body.Length : 4);
        if (defect == "unterminated")
        {
            body.AsSpan(4).Fill((byte)'x');
        }
        Assert.Throws<InvalidDataException>(() => MngrpArchive.Read(Files(87, body), Encoding.UTF8));
    }

    [Fact]
    public void CrossTablePointerOutsideParentIsRejected()
    {
        byte[] body = new byte[2048];
        Word(body, 0, 1);
        Word(body, 2, 4);
        Word(body, 4, 1);
        Word(body, 6, body.Length - 4);
        Assert.Throws<InvalidDataException>(() => MngrpArchive.Read(Files(2, body), Encoding.UTF8));
    }

    private static MngrpFilePair Files(int slot, byte[] content)
    {
        byte[] header = new byte[2048];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(slot * 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(slot * 8 + 4), checked((uint)content.Length));
        return new(content, header);
    }

    private static void Word(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), checked((ushort)value));
}
