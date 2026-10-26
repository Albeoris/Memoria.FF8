namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionUnusedFieldMemberTests
{
    [Fact]
    public void Noncanonical_runtime_filename_is_diagnosed_without_aliasing_or_discarding_valid_siblings()
    {
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".");
        ModFile unused = new(package.Id, ModFormat.Junction, "field/mapdata/ro/room/room_en.sfx", 1, () => []);
        ModFile valid = new(package.Id, ModFormat.Junction, "field/mapdata/ro/room/room.msd", 1, () => [0, 0, 0, 0]);
        ModFile metadata = new(package.Id, ModFormat.Junction, "field/mapdata/ro/room/room.sym", 1, () => []);
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [package], [unused, valid, metadata]), out var resources, out var diagnostics));
        Assert.Equal("field/room/room.msd", Assert.Single(resources).Key.Path);
        ModDiagnostic notice = Assert.Single(diagnostics);
        Assert.Equal("Junction.UnusedFieldMember", notice.Code);
        Assert.Equal(package.Id, notice.PackageId);
        Assert.Equal(unused.RelativePath, notice.RelativePath);
        Assert.Null(JunctionEncodedResources.Address(unused.RelativePath));
    }
}
