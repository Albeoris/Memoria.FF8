using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleConfuse36ControllerTests
{
    [Fact]
    public void EntryKeepsFiveDistinctPoolsAndWrappedTargetMaximum()
    {
        var fixture = Create();
        NativeBattleConfuse36Frame frame = fixture.Frame;
        Assert.Equal(new ushort[] { 0x64, 0x58, 0x2A4, 0x3C, 0x40 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 3, 3, 10 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleModulePoolTask root = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(0x26A8EA8);
        Assert.Equal(0x85F620u, root.Callback);
        Assert.Equal((byte)3, root.ReadByte(0x2C));
        Assert.Equal((byte)0, root.ReadByte(0x2D));
        Assert.Equal((ushort)1, root.ReadWord(0x5A));
        Assert.Equal(0x1C000, frame.State.AuxiliaryCursor);
        Assert.Null(frame.State.Emitters);
    }

    [Fact]
    public void ShakeTerminatesWithoutWritingItsSentinelToTheCamera()
    {
        var fixture = Create();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(4, 0x85FA30, 64);
        NativeBattleConfuse36Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = appended.Hierarchy.Pools } };
        List<short> writes = [];
        for (int index = 0; index < 5; index++)
        {
            frame = NativeBattleConfuse36Auxiliary.Advance(frame, appended.Address, fixture.Resources, (_, _) => throw new InvalidOperationException(), writes.Add).Frame;
        }
        Assert.Equal(new short[] { 16, -12, 8, -4 }, writes);
        Assert.Equal((byte)1, frame.Effect.Pools[4].Slots[0].ReadByte(0x29));
        Assert.Equal(1, frame.Effect.Pools[4].Slots[0].ReadByte(0x26) & 1);
    }

    [Fact]
    public void GenericSignedDelayDoesNotInitializeBeforeItsActualCounter()
    {
        var fixture = Create();
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Append8DC540(2, 0x85FB40, 676);
        NativeBattleEffectHierarchy hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteWord(0x298, 10).WriteWord(0x24, 9));
        NativeBattleConfuse36Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleConfuse36LeafStep step = NativeBattleConfuse36Generic.Advance(frame, appended.Address);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(appended.Address);
        Assert.Equal((ushort)10, task.ReadWord(0x24));
        Assert.Equal((byte)0, task.ReadByte(0x29));
        Assert.Equal(0, step.Result);
    }

    private static (NativeBattleConfuse36Frame Frame, NativeBattleConfuse36Resources Resources) Create()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 2, 11, -1, 39, 36, null));
        (packets, _) = packets.AppendContact(new(new(0, 1, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(36, 35, new(36, 0x85F4E0, 0x85F4C0), new(3, 1, 1, 0, 0, 0), null) { ResourcePointers = [new(0x26A8E7C, 0)] };
        NativeBattleConfuse36Resources resources = new(new("declared-camera", []), 0, [], [0x7F7F7F, 0x3F3F3F, 0xFF000000], [16, -12, 8, -4, 0x7F]);
        NativeBattleConfuse36Frame frame = NativeBattleConfuse36Controller.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(35), effect, packets, resources);
        return (frame, resources);
    }
}
