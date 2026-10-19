using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNoMercy200ControllerTests
{
    [Theory]
    [InlineData(6, 0x493E0)]
    [InlineData(35, 0x557C6)]
    public void Sound_uses_the_descriptor_at_each_original_immediate_address(ushort tick, uint descriptor)
    {
        var fixture = Fixture(tick);
        List<NativeBattleNoMercy200Operation> operations = [];
        NativeBattleNoMercy200ChildStep step = NativeBattleNoMercy200Controller.Advance639380(fixture.Frame, fixture.Pool, 0, Inputs(operations));
        Assert.Equal(descriptor, Assert.Single(operations.OfType<NativeBattleNoMercy200Sound>()).Descriptor);
        Assert.Equal((ushort)(tick + 1), step.Pool.Slots[0].ReadWord(12));
    }

    [Theory]
    [InlineData(45)]
    [InlineData(61)]
    [InlineData(122)]
    public void Pending_IO_stalls_the_original_tick_after_publishing_the_current_view(ushort tick)
    {
        var fixture = Fixture(tick);
        List<NativeBattleNoMercy200Operation> operations = [];
        NativeBattleNoMercy200Inputs inputs = Inputs(operations) with { ReadIoResult = () => -1 };
        NativeBattleNoMercy200ChildStep step = NativeBattleNoMercy200Controller.Advance639380(fixture.Frame, fixture.Pool, 0, inputs);
        Assert.Equal(tick, step.Pool.Slots[0].ReadWord(12));
        Assert.Empty(operations);
        Assert.Equal(65, BinaryPrimitives.ReadInt32LittleEndian(step.Frame.Staging.Read(0x24F9944, 4)));
    }

    [Fact]
    public void Paused_main_does_not_query_IO_but_still_updates_its_view_matrix()
    {
        var fixture = Fixture(35);
        List<NativeBattleNoMercy200Operation> operations = [];
        NativeBattleNoMercy200Inputs inputs = Inputs(operations) with { GraphicsFlags = 1, ReadIoResult = () => throw new InvalidOperationException() };
        NativeBattleNoMercy200ChildStep step = NativeBattleNoMercy200Controller.Advance639380(fixture.Frame, fixture.Pool, 0, inputs);
        Assert.Empty(operations);
        Assert.Equal((ushort)35, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(65, BinaryPrimitives.ReadInt32LittleEndian(step.Frame.Staging.Read(0x24F9944, 4)));
    }

    [Fact]
    public void Terminal_tick_keeps_both_native_zero_stage_writes_and_releases_after_increment()
    {
        var fixture = Fixture(174);
        List<NativeBattleNoMercy200Operation> operations = [];
        NativeBattleNoMercy200ChildStep step = NativeBattleNoMercy200Controller.Advance639380(fixture.Frame, fixture.Pool, 0, Inputs(operations));
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)175, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(new[] { 0, 0 }, operations.OfType<NativeBattleNoMercy200Stage>().Select(operation => operation.Intensity));
    }

    [Fact]
    public void Independent_restore_preserves_exact_next_sequence_spawn_and_stage_order()
    {
        var fixture = Fixture(21);
        NativeBattleNoMercy200Frame restored = JsonSerializer.Deserialize<NativeBattleNoMercy200Frame>(JsonSerializer.Serialize(fixture.Frame))!;
        List<NativeBattleNoMercy200Operation> first = [];
        List<NativeBattleNoMercy200Operation> second = [];
        NativeBattleNoMercy200ChildStep original = NativeBattleNoMercy200Controller.Advance639380(fixture.Frame, fixture.Pool, 0, Inputs(first));
        NativeBattleNoMercy200ChildStep clone = NativeBattleNoMercy200Controller.Advance639380(restored, restored.Effect.Pools[1], 0, Inputs(second));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(clone));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Collection(first, operation => Assert.Equal(new NativeBattleNoMercy200Sequence(2, 1), operation), operation => Assert.Equal(new NativeBattleNoMercy200Spawn(0x63A220), operation));
    }

    private static NativeBattleNoMercy200Inputs Inputs(List<NativeBattleNoMercy200Operation> operations) => new(0, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 0, 0, 4096), () => 0, (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });

    private static (NativeBattleNoMercy200Frame Frame, NativeBattleEffectTaskPool Pool) Fixture(ushort tick)
    {
        byte[] bytes = new byte[NativeBattleNoMercy200Resources.StaticLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(checked((int)(0xE33D30 - NativeBattleNoMercy200Resources.StaticAddress))), 0x493E0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(checked((int)(0xE33D34 - NativeBattleNoMercy200Resources.StaticAddress))), 0x557C6);
        NativeBattleNoMercy200Resources resources = new(new("test", bytes.ToImmutableArray()), [], new("test-tim", []), [], [], [], new(0xE33D8C, []));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(NativeBattleNoMercy200Controller.Root, 0x24F8D60, 16, 2);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleNoMercy200Controller.Children, 0x24F99A0, 36, 250);
        (pool, _) = pool.Append(0x639380);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(12, tick)) };
        byte[] matrix = new byte[32];
        foreach (int offset in new[] { 0, 8, 16 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt32LittleEndian(matrix.AsSpan(20), 55);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x24F8D90, matrix).Write(0x24F9930, new byte[32]).Write(0x24FBCE0, new byte[] { 2, 0, 0, 0 });
        NativeBattleOriginalEffectState effect = new(200, 199, new(200, 0x6391D0, 0x575530), new(2, 0, 8, 0, 0, 0), NativeBattleNoMercy200Controller.Root) { Pools = [root, pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { NoMercy200 = new(true) { Resources = resources } };
        return (new(memory, effect, staging, NativeCrtRandomState.FromColdProcess()), pool);
    }
}
