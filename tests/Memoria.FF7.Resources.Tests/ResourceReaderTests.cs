using System.Buffers.Binary;
using System.Text;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class ResourceReaderTests
{
    [Fact]
    public void LzssReadsLiteralsOverlappingCopiesAndZeroDictionary()
    {
        Assert.Equal("ABCABCABC", Encoding.ASCII.GetString(Lzss.DecodeRaw([7, 65, 66, 67, 0xEE, 0xF3])));
        Assert.Equal(new byte[3], Lzss.DecodeRaw([0, 0, 0]));
        Assert.Equal("AAAAAA", Encoding.ASCII.GetString(Lzss.DecodeRaw([1, 65, 0xEE, 0xF2])));
        Assert.Empty(Lzss.DecodeRaw([]));
        Assert.Equal(new byte[] { 65 }, Lzss.DecodeLengthFramed([2, 0, 0, 0, 1, 65]));
    }

    [Fact]
    public void LzssRejectsTruncationLengthMismatchAndOutputBomb()
    {
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeRaw([0]));
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeRaw([0, 1]));
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeLengthFramed([1, 0, 0, 0, 1, 65]));
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeRaw([0, 0, 0x0F], 17));
    }

    [Fact]
    public void LzssDictionaryWrapRetainsMostRecentWindow()
    {
        using MemoryStream compressed = new();
        byte[] expected = Enumerable.Range(0, 5000).Select(index => (byte)(index % 251)).ToArray();
        for (int i = 0; i < expected.Length; i += 8)
        {
            compressed.WriteByte(255);
            compressed.Write(expected, i, 8);
        }
        int reference = (0xFEE + expected.Length - 3) & 0xFFF;
        compressed.WriteByte(0);
        compressed.WriteByte((byte)reference);
        compressed.WriteByte((byte)((reference >> 4) & 0xF0));
        Assert.Equal(expected.Concat(expected[^3..]), Lzss.DecodeRaw(compressed.ToArray()));
    }

    [Fact]
    public void LgpPreservesDuplicateNamesConflictPathsAndDirectoryIdentity()
    {
        using MemoryStream source = BuildArchive();
        using LgpArchive archive = new(source, true);
        Assert.Equal(3, archive.Entries.Count);
        Assert.Equal(2, archive.Find("one/file").Count);
        Assert.Single(archive.Find("TWO\\FILE"));
        Assert.Equal(new byte[] { 1 }, archive.ReadEntry(archive.Entries[0]));
        Assert.Equal(new byte[] { 3 }, archive.ReadEntry(archive.Entries[2]));
        Assert.Throws<InvalidDataException>(() => archive.ReadEntry(archive.Entries[0], 0));
        Assert.Throws<ArgumentException>(() => archive.ReadEntry(archive.Entries[0] with { }));
        Assert.True(source.CanRead);
    }

    [Fact]
    public void LgpRejectsOversizedDirectoryInvalidConflictAndMemberBounds()
    {
        using MemoryStream source = BuildArchive();
        Assert.Throws<InvalidDataException>(() => new LgpArchive(source, true, 2));
        byte[] bytes = source.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16 + 81 + 3600 + 2 + 2 + 128), 90);
        Assert.Throws<InvalidDataException>(() => new LgpArchive(new MemoryStream(bytes)));
        bytes = source.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => new LgpArchive(new MemoryStream(bytes)));
        Assert.Throws<InvalidDataException>(() => new LgpArchive(new MemoryStream([0, 0])));
    }

    [Fact]
    public void PcFieldPreservesHeaderAndSectionPayloadAndRejectsOverlap()
    {
        byte[] decoded = new byte[42 + 9 * 5];
        BinaryPrimitives.WriteUInt16LittleEndian(decoded, 7);
        BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(2), 9);
        for (int i = 0; i < 9; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(6 + i * 4), (uint)(42 + i * 5));
            BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(42 + i * 5), 1);
            decoded[46 + i * 5] = (byte)i;
        }
        PcFieldFile field = PcFieldFile.ReadDecoded(decoded);
        Assert.Equal(7, field.ReservedHeader);
        Assert.Equal(new byte[] { 8 }, field.GetSection(PcFieldSectionKind.Background).ToArray());
        BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(10), 42);
        Assert.Throws<InvalidDataException>(() => PcFieldFile.ReadDecoded(decoded));
        Assert.Throws<InvalidDataException>(() => PcFieldFile.ReadDecoded(new byte[42]));
        BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(10), 47);
        BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(42), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => PcFieldFile.ReadDecoded(decoded));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiscoverySupportsClassicAndNestedLayoutsAndLocalizedRoles(bool nested)
    {
        string root = Path.Combine(Path.GetTempPath(), "Memoria.FF7.Resources.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            string data = nested ? Path.Combine(root, "ff7", "workingdir", "data") : Path.Combine(root, "data");
            Directory.CreateDirectory(Path.Combine(data, "field"));
            Directory.CreateDirectory(Path.Combine(data, "lang-de", "kernel"));
            File.WriteAllBytes(Path.Combine(data, "field", "gflevel.lgp"), []);
            File.WriteAllBytes(Path.Combine(data, "field", "char.lgp"), []);
            File.WriteAllBytes(Path.Combine(data, "lang-de", "kernel", "kernel.bin"), []);
            Ff7Installation installation = Ff7Installation.Discover(root, "de");
            Assert.Equal(nested ? Ff7InstallationLayout.SteamWorkingDirectory : Ff7InstallationLayout.ClassicPc, installation.Layout);
            Assert.Contains("lang-de", installation.GetRequired(Ff7ResourceRole.Kernel).Path);
            Assert.Throws<InvalidDataException>(() => Ff7Installation.Discover(root, "en"));
            Assert.Throws<NotSupportedException>(() => Ff7Installation.Discover(root, "unknown"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void OptionalInstalledOpeningFieldHasBoundedNineSectionContainer()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR to explicitly authorize read-only installed-resource validation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        LgpEntry entry = Assert.Single(archive.Find("md1stin"));
        PcFieldFile field = PcFieldFile.ReadCompressed(archive.ReadEntry(entry));
        Assert.Equal(9, field.Sections.Count);
        Assert.All(field.Sections, section => Assert.True(section.Length > 0));
        Console.WriteLine($"Profile {installation.Layout}/{installation.Language}; roles {installation.Resources.Count}; field archive members {archive.Entries.Count}; md1stin compressed {entry.Length}; decoded {field.DecodedLength}; header {field.ReservedHeader}.");
        foreach (PcFieldSection section in field.Sections)
        {
            Console.WriteLine($"{section.Kind}: header={section.HeaderOffset}, data={section.DataOffset}, length={section.Length}");
        }
        Ff7ResourceRole[] archiveRoles = [Ff7ResourceRole.CharacterArchive, Ff7ResourceRole.BattleArchive, Ff7ResourceRole.MagicArchive, Ff7ResourceRole.MenuArchive, Ff7ResourceRole.WorldArchive];
        foreach (Ff7ResourceRole role in archiveRoles)
        {
            using LgpArchive resource = LgpArchive.Open(installation.GetRequired(role).Path);
            Assert.NotEmpty(resource.Entries);
            Console.WriteLine($"{role}: members={resource.Entries.Count}, conflict-path members={resource.Entries.Count(member => member.ConflictGroup != 0)}");
        }
    }

    private static MemoryStream BuildArchive()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.ASCII, true);
        writer.Write((ushort)0);
        writer.Write(Encoding.ASCII.GetBytes("SQUARESOFT"));
        writer.Write(3);
        int dataStart = 16 + 81 + 3600 + 2 + 2 + 3 * 130;
        for (int i = 0; i < 3; i++)
        {
            Name("file", 20);
            writer.Write(dataStart + i * 25);
            writer.Write((byte)14);
            writer.Write((ushort)1);
        }
        writer.Write(new byte[3600]);
        writer.Write((ushort)1);
        writer.Write((ushort)3);
        for (int i = 0; i < 3; i++)
        {
            Name(i == 1 ? "two" : "one", 128);
            writer.Write((ushort)i);
        }
        for (int i = 0; i < 3; i++)
        {
            Name("file", 20);
            writer.Write(1);
            writer.Write((byte)(i + 1));
        }
        stream.Position = 0;
        return stream;

        void Name(string name, int length)
        {
            byte[] bytes = new byte[length];
            Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0);
            writer.Write(bytes);
        }
    }
}
