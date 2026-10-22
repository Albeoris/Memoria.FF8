using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurse47TargetSetupTests
{
    [Fact]
    public void Target_copies_two_actual_DWORDs_clears_shared84_and_preserves_geometry_and_lighting()
    {
        var fixture = Fixture(3);
        NativeBattleCurse47Frame result = NativeBattleCurse47TargetSetup.Initialize82B0F0(fixture.Frame, fixture.Address, actor =>
        {
            Assert.Equal(3, actor);
            return new(actor, 0) { Transform = new(new(999, 888, 777), new(0, -300, -100), -1234, 0, 4096) };
        });
        Assert.Equal(new byte[8], result.State.Read(NativeBattleCurse47State.SharedAddress, 8));
        Assert.Equal(unchecked((ushort)-300), result.State.SharedWord(10));
        Assert.Equal(unchecked((ushort)-100), result.State.SharedWord(12));
        Assert.Equal(unchecked((ushort)-1234), result.State.SharedWord(14));
        Assert.Equal((ushort)0, result.State.SharedWord(0x4A));
        Assert.Equal(new byte[58], result.State.Read(NativeBattleCurse47State.SharedAddress + 16, 58));
        Assert.Equal(new byte[8], result.State.Read(NativeBattleCurse47State.SharedAddress + 76, 8));
        Assert.Same(fixture.Frame.Memory, result.Memory);
        Assert.Same(fixture.Frame.Staging, result.Staging);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleCurse47Frame>(JsonSerializer.Serialize(result))));
    }

    [Theory]
    [InlineData(100, 0, 0xC00)]
    [InlineData(-100, 0, 0x400)]
    [InlineData(0, 100, 0x800)]
    [InlineData(0, -100, 0)]
    public void Target_heading_uses_signed_native_XZ_and_twelve_bit_subtraction(short x, short z, ushort heading)
    {
        var fixture = Fixture(0);
        NativeBattleCurse47Frame result = NativeBattleCurse47TargetSetup.Initialize82B0F0(fixture.Frame, fixture.Address, actor => new(actor, 0) { Transform = new(new(0, 0, 0), new(x, 0, z), 0, 0, 4096) });
        Assert.Equal(heading, result.State.SharedWord(0x4A));
    }

    [Fact]
    public void Unknown_shared_pointer_and_missing_actual_model_transform_fail_at_reached_reads()
    {
        var fixture = Fixture(3);
        Assert.Throws<NotSupportedException>(() => NativeBattleCurse47TargetSetup.Initialize82B0F0(fixture.Frame with { Staging = NativeBattleFileStaging.Unknown() }, fixture.Address, actor => new(actor, 0)));
        Assert.Throws<NotSupportedException>(() => NativeBattleCurse47TargetSetup.Initialize82B0F0(fixture.Frame, fixture.Address, actor => new(actor, 0)));
    }

    private static (NativeBattleCurse47Frame Frame, uint Address) Fixture(byte target)
    {
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleCurse47State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(1, NativeBattleCurse47Controller.TargetCallback, 88);
        hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteByte(0x2D, target));
        NativeBattleOriginalEffectState effect = new(47, 47, new(47, NativeBattleCurse47Controller.Entry, 0x82A7D0), new(0, 1, 47, 0, 0, 0), NativeBattleCurse47State.Pools[0].Descriptor) { Pools = hierarchy.Pools };
        NativeBattleCurse47State state = NativeBattleCurse47State.FromColdProcess(new byte[80].ToImmutableArray()).Write(NativeBattleCurse47State.SharedAddress, Enumerable.Repeat((byte)0xCC, 84).ToArray());
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x15B8028, BitConverter.GetBytes(NativeBattleCurse47State.SharedAddress));
        return (new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), state) { Staging = staging }, appended.Address);
    }
}
