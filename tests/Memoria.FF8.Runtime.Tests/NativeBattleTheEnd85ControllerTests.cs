using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85ControllerTests
{
    [Theory]
    [InlineData(0, 1, 255)]
    [InlineData(3, 1, 2)]
    [InlineData(1, 5, 4)]
    public void Entry_preserves_nine_physical_task_banks_and_unsigned_wrapped_contact_limit(byte targets, byte repeats, byte last)
    {
        var fixture = Fixture(targets, repeats);
        NativeBattleTheEnd85Frame frame = fixture.Frame;
        Assert.Equal(new[] { 2, 4, 3, 3, 6, 8, 60, 10, 2 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 88, 72, 676, 732, 96, 228, 64, 48 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(last, Root(frame).ReadByte(47));
        Assert.Equal(0x1D99A78u, Root(frame).ReadDword(12));
        Assert.Equal((byte)3, Root(frame).ReadByte(45));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x10000, frame.Memory.TheEnd85!.Dword(0x25C1B5C));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x18000, frame.Memory.TheEnd85.Dword(0x25C1B58));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x20000, frame.Memory.TheEnd85.Dword(0x25C21D8));
    }

    [Fact]
    public void Master_waits_on_previous_survivors_preserves_camera_padding_and_wraps_both_clocks()
    {
        var fixture = Fixture(1, 1);
        NativeBattleTheEnd85Frame frame = WithRoot(fixture.Frame, Root(fixture.Frame).WriteByte(41, 6).WriteWord(94, 1).WriteWord(92, ushort.MaxValue).WriteWord(36, ushort.MaxValue));
        frame = NativeBattleTheEnd85Controller.Advance784630(frame, fixture.Inputs);
        Assert.Equal((byte)6, Root(frame).ReadByte(41));
        Assert.Equal((ushort)0, Root(frame).ReadWord(94));
        Assert.Equal((ushort)0, Root(frame).ReadWord(92));
        Assert.Equal((ushort)0, Root(frame).ReadWord(36));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x10000, frame.Memory.TheEnd85!.Dword(0x25BC394));
        Assert.False(frame.Memory.SharedEffectView2793E58!.Known[18]);
        Assert.False(frame.Memory.SharedEffectView2793E58.Known[19]);
        NativeBattleTheEnd85Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleTheEnd85Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        frame = NativeBattleTheEnd85Controller.Advance784630(frame, fixture.Inputs);
        Assert.Equal((byte)7, Root(frame).ReadByte(41));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x18000, frame.Memory.TheEnd85!.Dword(0x25BC394));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleTheEnd85Controller.Advance784630(restored, fixture.Inputs))));
    }

    [Fact]
    public void A_leaf_that_releases_its_own_hierarchy_decrements_the_parent_once()
    {
        var fixture = Fixture(1, 1);
        NativeBattleTheEnd85Frame frame = WithRoot(fixture.Frame, Root(fixture.Frame).WriteByte(41, 6).WriteWord(94, 1));
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(7, 0x8DDC30, 64, NativeBattleTheEnd85Controller.MainBank, (pointer, header, target) => NativeBattleCureController.SelectActors(frame.Effect.Parameters!, fixture.Inputs.Packets, pointer, header, target));
        frame = frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } };
        NativeBattleTheEnd85Inputs inputs = fixture.Inputs with
        {
            AdvanceLeaf = (current, address) =>
            {
                Assert.Equal(appended.Address, address);
                NativeBattleEffectHierarchy released = new NativeBattleEffectHierarchy(current.Effect.Pools).Complete8DC530(address);
                return new(current with { Effect = current.Effect with { Pools = released.Pools } }, true) { HierarchyReleased = true };
            }
        };
        frame = NativeBattleTheEnd85Controller.Advance784630(frame, inputs);
        Assert.Equal((byte)0, Root(frame).ReadByte(0x28));
        Assert.Equal((ushort)0, Root(frame).ReadWord(0x5E));
        Assert.Null(frame.Effect.Pools[7].Head);
    }

    private static NativeBattleModulePoolTask Root(NativeBattleTheEnd85Frame frame) => frame.Effect.Pools[0].Slots[0];
    private static NativeBattleTheEnd85Frame WithRoot(NativeBattleTheEnd85Frame frame, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(NativeBattleTheEnd85Controller.MainBank, task).Pools } };
    private static (NativeBattleTheEnd85Frame Frame, NativeBattleTheEnd85Inputs Inputs) Fixture(byte targets, byte repeats)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 42, 0, 85, 1, null));
        for (int index = 0; index < Math.Max(1, (int)targets); index++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)(3 + index), 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        packets = packets.WritePlayerRepeatMode(0, repeats);
        (packets, _) = packets.Publish(targets);
        NativeBattleOriginalEffectState effect = new(85, 85, new(85, 0x7844A0, 0x784480), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x25BC390, 0x10000)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), TheEnd85 = NativeBattleTheEnd85State.FromColdProcess() };
        NativeBattleTheEnd85Frame frame = NativeBattleTheEnd85Controller.Initialize784480(new(memory, effect, new(1)));
        frame = NativeBattleTheEnd85Controller.Initialize7844A0(frame, packets);
        NativeBattlePackedMatrix32 camera = new(Enumerable.Range(0, 32).Select(index => (byte)index).ToImmutableArray(), Enumerable.Repeat(true, 32).ToImmutableArray().SetItem(18, false).SetItem(19, false));
        NativeBattleTheEnd85Inputs inputs = new(packets, camera, index => new(index, 0), _ => throw new InvalidOperationException("Unloaded caster cannot query pose resources."), (_, _) => throw new InvalidOperationException("Target boot was not reached."), (_, _) => throw new InvalidOperationException("No leaf was admitted."), (_, _) => throw new InvalidOperationException("Contact was not reached."), (_, _) => throw new InvalidOperationException("Resource request was not reached."), 0x25C1C60);
        return (frame, inputs);
    }
}
