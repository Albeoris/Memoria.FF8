using System.Buffers.Binary;
using System.Text;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionFieldArchiveGroupTests
{
    [Theory]
    [InlineData("field/mapdata/ro/room.fi", "field/room.fi")]
    [InlineData("DIRECT\\FIELD\\MAPDATA\\RO\\ROOM.FS", "field/room.fs")]
    [InlineData("field/mapdata/xx/room.fl", null)]
    [InlineData("field/mapdata/ro/other/room.fl", null)]
    [InlineData("field/mapdata/ro/room.xyz", null)]
    public void ArchivePathsHaveTheSameCanonicalFieldIdentity(string path, string? expected)
    {
        Assert.Equal(expected, JunctionEncodedResources.Address(path)?.Path);
        if (expected is not null)
        {
            Assert.Equal("room", JunctionEncodedResources.FieldGroup(new(expected)));
        }
    }

    [Fact]
    public void ArchivePublishesFrozenTupleAndEveryMemberUnderAnExclusiveScope()
    {
        ModFile[] archive = Archive("first", ("jsm", (byte)1), ("msd", (byte)2), ("sym", (byte)3));
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], archive), out var resources, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(6, resources.Count);
        Assert.All(resources, resource => Assert.Equal(new ResourceKey("field/room"), resource.ExclusiveScope));
        Assert.Equal(new byte[] { 3 }, Read(resources.Single(resource => resource.Key.Path == "field/room/room.sym")));
        Assert.StartsWith("first:field/mapdata/ro/room.fs", resources.Single(resource => resource.Key.Path == "field/room/room.jsm").Content.Provenance);
    }

    [Fact]
    public void LooseHigherPriorityMemberRejectsTheCompleteLowerArchive()
    {
        ModFile[] files = [Loose("first", "jsm", 9), .. Archive("second", ("jsm", (byte)1), ("msd", (byte)2))];
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], files), out var resources, out var diagnostics));
        Assert.Equal("field/room/room.jsm", Assert.Single(resources).Key.Path);
        Assert.Null(resources[0].ExclusiveScope);
        Assert.Equal("second", Assert.Single(diagnostics).PackageId);
    }

    [Fact]
    public void DirectoryConflictRejectsLowerArchiveWithoutOpeningItsDataOrIndex()
    {
        ModFile[] archive = Archive("second", ("jsm", (byte)1), ("msd", (byte)2));
        ModFile[] files = [Loose("first", "jsm", 9), archive[0], new("second", ModFormat.Junction, "field/mapdata/ro/room.fi", 1, () => throw new IOException("Rejected index must remain unread.")), new("second", ModFormat.Junction, "field/mapdata/ro/room.fs", 1, () => throw new IOException("Rejected data must remain unread."))];
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], files), out var resources, out var diagnostics));
        Assert.Single(resources);
        Assert.Equal("Junction.FieldGroupConflict", Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void ArchiveRejectsTheWholeLowerLooseGroupAndDoesNotKeepItsRatTail()
    {
        ModFile[] files = [.. Archive("first", ("jsm", (byte)1), ("msd", (byte)2)), Loose("second", "jsm", 8), Loose("second", "rat", 9)];
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], files), out var resources, out var diagnostics));
        Assert.Equal(5, resources.Count);
        Assert.DoesNotContain(resources, resource => resource.Content.Format == "rat");
        Assert.Equal("second", Assert.Single(diagnostics).PackageId);
    }

    [Fact]
    public void DisjointExplicitContributionCanFillAnOmittedArchiveMember()
    {
        ModFile[] files = [.. Archive("first", ("jsm", (byte)1), ("msd", (byte)2)), Loose("second", "rat", 9)];
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], files), out var resources, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(6, resources.Count);
        Assert.StartsWith("second:", resources.Single(resource => resource.Content.Format == "rat").Content.Provenance);
    }

    [Fact]
    public void TuplePartsCannotBeBorrowedFromAnotherPackage()
    {
        ModFile[] archive = Archive("first", ("jsm", (byte)1));
        ModFile borrowed = Archive("second", ("jsm", (byte)2)).Single(file => file.RelativePath.EndsWith(".fs", StringComparison.Ordinal));
        Assert.False(JunctionEncodedResources.TryPrepare(new(1, [], [archive[0], archive[1], borrowed]), out var resources, out var diagnostics));
        Assert.Empty(resources);
        Assert.Equal("first", Assert.Single(diagnostics).PackageId);
        Assert.Contains("same package", diagnostics[0].Message);
    }

    [Fact]
    public void AcceptedTuplePreventsReadingLowerOverlappingTuple()
    {
        ModFile[] files = [.. Archive("first", ("jsm", (byte)1)), new("second", ModFormat.Junction, "field/mapdata/ro/room.fi", 1, () => throw new IOException("Rejected tuple must remain unread."))];
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], files), out var resources, out var diagnostics));
        Assert.Equal(4, resources.Count);
        Assert.Equal("Junction.FieldGroupConflict", Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void CorruptWinningTupleDoesNotFallThroughToAnotherPackage()
    {
        ModFile[] files = [new("first", ModFormat.Junction, "field/mapdata/ro/room.fi", 1, () => throw new IOException("Unavailable index.")), .. Archive("second", ("jsm", (byte)2))];
        Assert.False(JunctionEncodedResources.TryPrepare(new(1, [], files), out var resources, out var diagnostics));
        Assert.Empty(resources);
        Assert.Equal("first", Assert.Single(diagnostics).PackageId);
    }

    private static byte[] Read(EncodedResource resource)
    {
        using Stream stream = resource.Content.OpenRead();
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static ModFile Loose(string package, string extension, byte value) => new(package, ModFormat.Junction, $"field/mapdata/ro/room/room.{extension}", 1, () => [value]);

    private static ModFile[] Archive(string package, params (string Extension, byte Value)[] members)
    {
        byte[] listing = Encoding.ASCII.GetBytes(string.Concat(members.Select(member => $"c:\\ff8\\data\\author\\room.{member.Extension}\r\n")));
        byte[] index = new byte[members.Length * 12];
        byte[] content = members.Select(member => member.Value).ToArray();
        for (int number = 0; number < members.Length; number++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(index.AsSpan(number * 12), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(index.AsSpan(number * 12 + 4), (uint)number);
        }
        return new[] { ("fl", listing), ("fi", index), ("fs", content) }.Select(part => new ModFile(package, ModFormat.Junction, $"field/mapdata/ro/room.{part.Item1}", 1, () => part.Item2)).ToArray();
    }
}
