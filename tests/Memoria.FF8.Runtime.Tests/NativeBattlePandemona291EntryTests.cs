using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattlePandemona291EntryTests
{
    [Fact]
    public void EntryQueuesBothStaticTimsAndUsesSignedCentroidBeforeRootPublication()
    {
        var fixture = Fixture();
        NativeBattlePandemona291Frame result = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, fixture.Resources, fixture.Packets, ReadModel);
        NativeBattlePandemona291State state = result.Memory.Pandemona291!;
        Assert.Equal(-1, state.Ground25562E0);
        Assert.Equal(3999, state.Center25562E8);
        Assert.Equal(0u, state.Scratch25562EC);
        Assert.Equal(17u, state.Pause2556258);
        Assert.Equal(3u, state.StaticLoadState13BBBE8);
        Assert.Equal(0x1D972C0u + 3 * 0x9Cu, state.Caster2556260);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress, state.ModelBuffer25562F4);
        NativeBattleEffectTextureState textures = result.Memory.Textures!;
        Assert.Equal(2, textures.Count);
        Assert.Same(fixture.Resources.FirstTim, textures.Slots[0]!.Source);
        Assert.Same(fixture.Resources.SecondTim, textures.Slots[1]!.Source);
        NativeBattleEffectTaskPool pool = Assert.Single(result.Effect.Pools);
        Assert.Equal(0x25562B8u, result.Effect.ReturnedDescriptor);
        Assert.Equal(0x25562C8u, pool.BankAddress);
        Assert.Equal(0x6ED350u, Assert.Single(pool.Slots).Callback);
        Assert.Equal(new byte[8], NativeBattleStorageSnapshots.Capture(pool.Slots[0]).Payload);
        Assert.Equal(fixture.Frame.Random, result.Random);
        Assert.Equal(0, fixture.Frame.Memory.Textures!.Count);
    }

    [Fact]
    public void EntryStandaloneSnapshotReproducesTheSameCurrentStateWithoutDefaultingWarmOwners()
    {
        var fixture = Fixture();
        NativeBattlePandemona291Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePandemona291Frame>(NativeBattleTestSnapshots.Json(fixture.Frame))!;
        var live = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, fixture.Resources, fixture.Packets, ReadModel);
        var resumed = NativeBattlePandemona291Entry.Initialize6ED250(restored, fixture.Resources, fixture.Packets, ReadModel);
        Assert.Equal(NativeBattleTestSnapshots.Json(live), NativeBattleTestSnapshots.Json(resumed));
        Assert.Throws<NotSupportedException>(() => NativeBattlePandemona291Entry.Initialize6ED250(restored with { Memory = restored.Memory with { Pandemona291 = null } }, fixture.Resources, fixture.Packets, ReadModel));
    }

    [Fact]
    public void MasterFlipsTheActualPacketCursorWhilePauseFreezesOnlyTheCounter()
    {
        var fixture = Fixture();
        NativeBattlePandemona291Frame frame = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, fixture.Resources, fixture.Packets, ReadModel);
        List<(uint, int)> flashes = [];
        for (int index = 0; index < 2; index++)
        {
            frame = NativeBattlePandemona291Controller.Advance6ED350(frame, (depth, flags) => flashes.Add((depth, flags)));
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + (index == 0 ? 0x151D4u : 0x71D4u), frame.Memory.Pandemona291!.PacketCursor25562DC);
            Assert.Equal((ushort)0, frame.Effect.Pools[0].Slots[0].ReadWord(0xC));
        }
        Assert.Equal(new[] { (0u, 0), (0u, 0) }, flashes);
        frame = frame with { Memory = frame.Memory with { Pandemona291 = frame.Memory.Pandemona291! with { Pause2556258 = 0 } } };
        for (int index = 0; index < 2; index++)
        {
            frame = NativeBattlePandemona291Controller.Advance6ED350(frame, (_, _) => { });
        }
        Assert.Equal((ushort)2, frame.Effect.Pools[0].Slots[0].ReadWord(0xC));
        NativeBattlePandemona291Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePandemona291Frame>(NativeBattleTestSnapshots.Json(frame))!;
        string before = NativeBattleTestSnapshots.Json(restored);
        Assert.Throws<NotSupportedException>(() => NativeBattlePandemona291Controller.Advance6ED350(restored, (_, _) => throw new InvalidOperationException()));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Constructed_master_publishes_alternating_cursor_before_children_and_preserves_the_toggle_afterwards()
    {
        var fixture = Fixture();
        NativeBattlePandemona291Frame frame = NativeBattlePandemona291Entry.Initialize6ED250(fixture.Frame, fixture.Resources, fixture.Packets, ReadModel);
        NativeBattleEffectTaskPool master = frame.Effect.Pools[0];
        NativeBattleModulePoolTask root = master.Slots[0].WriteByte(0xF, 1).WriteDword(0x10, 0);
        master = NativeBattleTaskFixture.EditPool(master, data => data with { Slots = master.Slots.SetItem(0, root) });
        ImmutableArray<NativeBattleEffectTaskPool> children = new uint[] { 0x25562A8, 0x2556298, 0x2556288, 0x2556278, 0x2556268 }.Select((descriptor, index) => NativeBattleEffectTaskPool.Initialize(descriptor, 0x20DFAB8u + (uint)(index * 0x100), 0x20, 1)).ToImmutableArray();
        var spawned = children[0].Append(0x6ED900);
        children = children.SetItem(0, spawned.Item1);
        frame = frame with { Effect = frame.Effect with { Pools = children.Insert(0, master) } };
        NativeBattlePandemona291ConstructionInputs construction = new(fixture.Packets, ReadModel, (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        List<uint> cursors = [];
        NativeBattlePandemona291MasterInputs inputs = new(construction, (current, _, task) =>
        {
            uint cursor = current.Memory.Pandemona291!.PacketCursor25562DC!.Value;
            cursors.Add(cursor);
            return new(current with { Memory = current.Memory with { Pandemona291 = current.Memory.Pandemona291 with { PacketCursor25562DC = cursor + 52 } } }, task, 0);
        });
        for (int index = 0; index < 2; index++)
        {
            frame = NativeBattlePandemona291Controller.Advance6ED350(frame, (_, _) => { }, inputs);
            Assert.Equal((uint)(index == 0 ? 1 : 0), frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x25562B8).Slots[0].ReadDword(0x10));
        }
        Assert.Equal(new[] { 0x20DFAB8u + 0x151D4u, 0x20DFAB8u + 0x71D4u }, cursors);
    }

    private static NativeBattleGraphicsModel ReadModel(int index) => new(index, 0) { Transform = new(new(0, 0, 0), new(0, 0, checked((short)(index == 0 ? -103 : 100))), 0, 0, 0) };

    private static (NativeBattlePandemona291Frame Frame, NativeBattlePandemona291Resources Resources, NativeBattleActionPacketsState Packets) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(3, 240, 0, 0, 3, 291, null));
        foreach (byte actor in new byte[] { 0, 1 })
        {
            (packets, _) = packets.AppendContact(new(new(actor, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(2);
        NativeBattleOriginalEffectState effect = new(291, 290, new(291, 0x6ED250, 0), new(3, 0, 3, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Pandemona291 = NativeBattlePandemona291State.FromColdProcess() with { Pause2556258 = 17, Scratch25562EC = 123 } };
        NativeBattlePandemona291Resources resources = new(Tim(0x31620, 5644, "first"), Tim(0x2220, 524, "second"));
        return (new(memory, effect, NativeCrtRandomState.FromColdProcess()), resources, packets);
    }

    private static NativeBattleEffectHeapAllocation Tim(int length, int clutLength, string identity)
    {
        byte[] data = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 9);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), clutLength);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8 + clutLength), length - 8 - clutLength);
        return new(identity, data.ToImmutableArray());
    }
}
