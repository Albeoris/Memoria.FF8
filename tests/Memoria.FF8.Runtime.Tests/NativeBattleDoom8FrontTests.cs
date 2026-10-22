using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8FrontTests
{
    [Fact]
    public void Observed_mode_two_creates_all_five_children_and_initializes_reaper_before_particle_births()
    {
        var fixture = Fixture(2);
        bool initialized = false;
        NativeBattleDoom8Frame result = NativeBattleDoom8Fronts.SpawnObservedMode(fixture.Frame, fixture.Director, fixture.Packets, 0x32808080, (frame, operation) =>
        {
            NativeBattleDoom8ReaperInitializeModel initialize = Assert.IsType<NativeBattleDoom8ReaperInitializeModel>(operation);
            Assert.Equal(0x1636CE8u, initialize.ResourceAddress);
            Assert.Equal((ushort)3, initialize.Argument3);
            Assert.Equal(0x32808080u, initialize.DefaultColor44);
            Assert.Equal((byte)2, NativeBattleDoom8Controller.Read(frame, fixture.Director).ReadByte(0x28));
            Assert.DoesNotContain(frame.Effect.Pools[5].Slots, task => (task.Flags & 1) != 0);
            Assert.DoesNotContain(frame.Effect.Pools[6].Slots, task => (task.Flags & 1) != 0);
            initialized = true;
            return frame with { State = frame.State.WriteDword(0x275F9D4, 17) };
        });
        Assert.True(initialized);
        Assert.Equal(17u, result.State.ReadDword(0x275F9D4));
        Assert.Equal((byte)5, NativeBattleDoom8Controller.Read(result, fixture.Director).ReadByte(0x28));
        NativeBattleModulePoolTask generic = Assert.Single(result.Effect.Pools[3].Slots, task => (task.Flags & 1) != 0);
        Assert.Equal(NativeBattleDoom8Nodes.Callback, generic.Callback);
        Assert.Equal(0x1818B78u, generic.ReadDword(0x30));
        Assert.Equal((ushort)0, generic.ReadWord(0x298));
        Assert.Equal((ushort)45, generic.ReadWord(0x29E));
        Assert.Equal((ushort)2, generic.ReadWord(0x29C));
        Assert.Equal(new uint[] { 0x8CD4B0, 0x8CC1E0 }, result.Effect.Pools[5].Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
        Assert.Equal(0x8CDED0u, Assert.Single(result.Effect.Pools[6].Slots, task => (task.Flags & 1) != 0).Callback);
        Assert.Equal(fixture.Frame.Random, result.Random);
    }

    [Fact]
    public void Mode_four_uses_actual_control124_and_original_ACT_overlay_and_stage_fields()
    {
        var fixture = Fixture(4);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(NativeBattleDoom8Io.NextAddressGlobal, 0x20FFAB8).InitializeIo().WriteDword(NativeBattleDoom8Io.ControlAddress + 0x124, 0x19EBABC) };
        NativeBattleDoom8Frame result = NativeBattleDoom8Fronts.SpawnObservedMode(frame, fixture.Director, fixture.Packets, 0, (_, _) => throw new InvalidOperationException("Mode four has no reaper initialization."));
        NativeBattleModulePoolTask act = Assert.Single(result.Effect.Pools[4].Slots, task => (task.Flags & 1) != 0);
        Assert.Equal(0x8C58B0u, act.Callback);
        Assert.Equal(0x19EBABCu, act.ReadDword(0x74));
        Assert.Equal(0xC0u, act.ReadDword(0x78));
        Assert.Equal((ushort)0, act.ReadWord(0x80));
        Assert.Equal((ushort)0, act.ReadWord(0x82));
        NativeBattleModulePoolTask overlay = Assert.Single(result.Effect.Pools[7].Slots, task => (task.Flags & 1) != 0);
        Assert.Equal(0x8DDC30u, overlay.Callback);
        Assert.Equal(0x163ACB4u, overlay.ReadDword(0x34));
        Assert.Equal((ushort)26, overlay.ReadWord(0x38));
        Assert.Equal(0x8CC2D0u, Assert.Single(result.Effect.Pools[5].Slots, task => (task.Flags & 1) != 0).Callback);
        Assert.Equal((byte)3, NativeBattleDoom8Controller.Read(result, fixture.Director).ReadByte(0x28));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Other_observed_modes_do_not_read_IO_or_invoke_lower_services(short mode)
    {
        var fixture = Fixture(mode);
        Assert.Same(fixture.Frame, NativeBattleDoom8Fronts.SpawnObservedMode(fixture.Frame, fixture.Director, fixture.Packets, 0, (_, _) => throw new InvalidOperationException()));
    }

    [Fact]
    public void Mode_four_rejects_missing_IO_pointer_and_mode_two_requires_real_initialization_service()
    {
        var fixture = Fixture(4);
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Fronts.SpawnObservedMode(fixture.Frame, fixture.Director, fixture.Packets, 0, (frame, _) => frame));
        fixture = Fixture(2);
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Fronts.SpawnObservedMode(fixture.Frame, fixture.Director, fixture.Packets, 0, (_, _) => throw new NotSupportedException("Actual model initializer unavailable.")));
    }

    [Fact]
    public void ACT_constructor_sign_extends_workspace_word_and_writes_only_its_native_arguments_after_allocation()
    {
        var fixture = Fixture(4);
        NativeBattleDoom8FrontAppend appended = NativeBattleDoom8Fronts.Append8C5860(fixture.Frame, fixture.Director, 0x8C8790, 0x12345678, -1, -2, -3, (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(appended.Frame, appended.Address);
        Assert.Equal(0x12345678u, task.ReadDword(0x74));
        Assert.Equal(0xFFFFFFFFu, task.ReadDword(0x78));
        Assert.Equal((ushort)0xFFFE, task.ReadWord(0x80));
        Assert.Equal((ushort)0xFFFD, task.ReadWord(0x82));
        Assert.Equal((byte)0, task.ReadByte(0x29));
        Assert.Equal(0u, task.ReadDword(0x84));
        Assert.Equal((byte)1, NativeBattleDoom8Controller.Read(appended.Frame, fixture.Director).ReadByte(0x28));
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8Fronts.Append8C5860(fixture.Frame, fixture.Director, 0xDEADBEEF, 0, 0, 0, 0, (_, _, _) => new(0, 3)));
    }

    private static (NativeBattleDoom8Frame Frame, uint Director, NativeBattleActionPacketsState Packets) Fixture(short mode)
    {
        var prior = NativeBattleRaldo19ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleDoom8Controller.RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), prior.Frame.Effect.Parameters!, prior.Inputs.Packets));
        NativeBattleEffectHierarchyAppend director = hierarchy.Append8DC540(2, NativeBattleDoom8Controller.DirectorCallback, 72, root.Address, (_, _, _) => new(0, 3));
        NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), prior.Frame.Effect.Parameters, NativeBattleDoom8State.Pools[0].Descriptor) { Pools = director.Hierarchy.Pools };
        NativeBattleDoom8Frame frame = new(prior.Frame.Memory, effect, new(123), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray()).WithSharedWord(0x40, unchecked((ushort)mode)));
        return (frame, director.Address, prior.Inputs.Packets);
    }
}
