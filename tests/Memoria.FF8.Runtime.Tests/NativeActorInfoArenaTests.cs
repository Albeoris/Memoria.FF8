using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeActorInfoArenaTests
{
    [Fact]
    public void Only_the_complete_physical_read_inside_the_certified_zero_range_resolves()
    {
        NativeActorInfoArenaDefinition definition = Definition();
        Assert.True(definition.TryReadZero(400, 255, 0, 4, out int first));
        Assert.Equal(0, first);
        Assert.False(definition.TryReadZero(399, 255, 0, 4, out _));
        Assert.True(definition.TryReadZero(255 * 436, 255, 577, 1, out int last));
        Assert.Equal(0, last);
        Assert.False(definition.TryReadZero(255 * 436, 255, 577, 2, out _));
        Assert.False(definition.TryReadZero(255 * 436, 255, 578, 1, out _));
        Assert.False(definition.TryReadZero(0, 0, 0, 4, out _));
        Assert.False(definition.TryReadZero(0, 255, 0, 4, out _));
    }

    [Fact]
    public void The_physical_address_determines_the_result_for_every_body_index()
    {
        NativeActorInfoArenaDefinition definition = Definition();
        Assert.True(definition.TryReadZero(400 + 612, 254, 0, 4, out int value));
        Assert.Equal(0, value);
        Assert.True(definition.TryReadZero(400, 255, 4, 2, out _));
        Assert.True(definition.TryReadZero(404, 255, 0, 2, out _));
        Assert.False(definition.TryReadZero(400, 254, 0, 4, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(int.MaxValue)]
    public void Unproved_declared_record_counts_refuse(int maximum)
    {
        Assert.Throws<InvalidDataException>(() => (Definition() with { MaximumDeclaredRecords = maximum }).Validate());
    }

    [Fact]
    public void Version_executable_and_resource_write_extents_require_certification()
    {
        Assert.Throws<InvalidDataException>(() => (Definition() with { Version = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (Definition() with { ExecutableHash = new string('0', 64) }).Validate());
        Assert.Throws<InvalidDataException>(() => (Definition() with { KernelBytes = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (Definition() with { WorldTableBytes = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (Definition() with { KernelBytes = -1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (Definition() with { WorldTableBytes = -1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (Definition() with { KernelBytes = int.MaxValue }).Validate());
        Assert.Throws<InvalidDataException>(() => (Definition() with { WorldTableBytes = int.MaxValue }).Validate());
        (Definition() with { KernelBytes = 0x18B1B44 - 0x188B018, WorldTableBytes = 0x18B1B44 - (0x1893018 - 1728) }).Validate();
        Assert.Throws<InvalidDataException>(() => (Definition() with { KernelBytes = 0x18B1B44 - 0x188B018 + 1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (Definition() with { WorldTableBytes = 0x18B1B44 - (0x1893018 - 1728) + 1 }).Validate());
    }

    [Fact]
    public void Invalid_prefix_record_offsets_and_widths_refuse_before_reading()
    {
        NativeActorInfoArenaDefinition definition = Definition() with { MaximumDeclaredRecords = 45 };
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.TryReadZero(-1, 255, 0, 4, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.TryReadZero(45 * 436 + 1, 255, 0, 4, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => Definition().TryReadZero(255 * 436 + 1, 255, 0, 4, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.TryReadZero(400, 255, -1, 4, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.TryReadZero(400, 255, 609, 4, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.TryReadZero(400, 255, int.MaxValue, 4, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.TryReadZero(400, 255, 0, 3, out _));
    }

    [Fact]
    public void Snapshot_definition_preserves_the_authenticated_bounds()
    {
        NativeActorInfoArenaDefinition definition = Definition() with { MaximumDeclaredRecords = 45 };
        NativeActorInfoArenaDefinition restored = JsonSerializer.Deserialize<NativeActorInfoArenaDefinition>(JsonSerializer.Serialize(definition))!;
        restored.Validate();
        Assert.Equal(definition, restored);
        Assert.True(restored.TryReadZero(400, 255, 0, 4, out int value));
        Assert.Equal(0, value);
    }

    private static NativeActorInfoArenaDefinition Definition()
    {
        return new NativeActorInfoArenaDefinition(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, 255, 37992, 1728);
    }
}
