using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaNodeAllocationTests
{
    private const uint ArenaAddress = 0x20DFAB8;
    private const int First = 0xC000;
    private const int Second = First + 0x25F8;

    [Fact]
    public void Mixed_native_lists_link_actual_emitter_and_geometry_storage_and_keep_removed_link_bytes()
    {
        var fixture = Fixture();
        NativeBattleCuragaNodeAllocation emitter = NativeBattleCuragaNodes.AllocateEmitter881700(fixture.Frame, fixture.Address, 0, 5, 3);
        Assert.Equal(First, emitter.ArenaOffset);
        NativeBattleCuragaNodeAllocation geometry = NativeBattleCuragaNodes.AllocateGeometry882080(emitter.Frame, fixture.Address, First, 9);
        Assert.Equal(Second, geometry.ArenaOffset);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(geometry.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal((ushort)1, task.ReadWord(0x34 + 0x14));
        Assert.Equal((ushort)1, task.ReadWord(0x34 + 0x16));
        Assert.Equal(ArenaAddress + First, task.ReadDword(0x34 + 0x2C));
        Assert.Equal(ArenaAddress + Second, task.ReadDword(0x34 + 0x30));
        Assert.Equal(ArenaAddress + Second, Dword(geometry.Frame, First + 4));
        Assert.Equal(ArenaAddress + First, Dword(geometry.Frame, Second));
        Assert.Equal(ArenaAddress + First, Dword(geometry.Frame, Second + 0x1BC));
        Assert.Equal(new byte[] { 0, 0 }, geometry.Frame.Memory.Arena!.Read(First + 8, 2));
        Assert.Equal(new byte[] { 1, 0 }, geometry.Frame.Memory.Arena.Read(Second + 8, 2));
        Assert.Equal(new byte[] { 5, 3 }, geometry.Frame.Memory.Arena.Read(First + 0x6A, 2));
        Assert.Equal(new byte[] { 9, 1, 0, 3 }, geometry.Frame.Memory.Arena.Read(Second + 0x1D6, 4));
        NativeBattleCuragaFrame unlinked = NativeBattleCuragaNodes.Unlink882960(geometry.Frame, fixture.Address, First);
        task = new NativeBattleEffectHierarchy(unlinked.Effect.Pools).Read(fixture.Address);
        Assert.Equal(ArenaAddress + Second, task.ReadDword(0x34 + 0x2C));
        Assert.Equal(0u, Dword(unlinked, Second));
        Assert.Equal(ArenaAddress + Second, Dword(unlinked, First + 4));
        Assert.Equal((ushort)1, task.ReadWord(0x34 + 0x14));
        NativeBattleCuragaFrame clone = RoundTrip(unlinked);
        Assert.Equal(SnapshotJson(NativeBattleCuragaNodes.Unlink882960(unlinked, fixture.Address, Second)), SnapshotJson(NativeBattleCuragaNodes.Unlink882960(clone, fixture.Address, Second)));
    }

    [Fact]
    public void Occupied_emitter_scan_wraps_at_eighty_nine_even_when_ninetieth_physical_slot_is_free()
    {
        var fixture = Fixture();
        byte[] occupied = new byte[0x25F8];
        for (int index = 0; index < 89; index++)
        {
            occupied[index * 108 + 0x69] = 1;
        }
        NativeBattleCuragaFrame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = fixture.Frame.Memory.Arena!.Write(First, occupied) } };
        NativeBattleCuragaNodeAllocation result = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Address, 0, 5, 0);
        Assert.Null(result.ArenaOffset);
        Assert.Equal((ushort)2, result.Frame.State.Counter26D76C0);
        Assert.Equal(new byte[] { 0 }, result.Frame.Memory.Arena!.Read(First + 89 * 108 + 0x69, 1));
        Assert.Equal((ushort)0, new NativeBattleEffectHierarchy(result.Frame.Effect.Pools).Read(fixture.Address).ReadWord(0x34 + 0x14));
    }

    [Fact]
    public void Occupied_geometry_scan_wraps_at_one_hundred_thirty_nine_even_when_last_physical_slot_is_free()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaNodes.AllocateEmitter881700(fixture.Frame, fixture.Address, 0, 5, 0).Frame;
        byte[] occupied = new byte[0x16F80];
        for (int index = 0; index < 139; index++)
        {
            occupied[index * 672 + 0x1D7] = 1;
        }
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Second, occupied) } };
        NativeBattleCuragaNodeAllocation result = NativeBattleCuragaNodes.AllocateGeometry882080(frame, fixture.Address, First, 5);
        Assert.Null(result.ArenaOffset);
        Assert.Equal((ushort)2, result.Frame.State.Counter26D82E8);
        Assert.Equal(new byte[] { 0 }, result.Frame.Memory.Arena!.Read(Second + 139 * 672 + 0x1D7, 1));
    }

    [Fact]
    public void Emitter_lifetime_decrements_signed_WORD_and_unlinks_before_clearing_its_actual_live_flag()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaNodes.AllocateEmitter881700(fixture.Frame, fixture.Address, 0, 5, 0).Frame;
        frame = NativeBattleCuragaNodes.AdvanceEmitterLifetime8817F0(frame, fixture.Address, First);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal(new byte[] { 255, 255 }, frame.Memory.Arena!.Read(First + 0x64, 2));
        Assert.Equal(new byte[] { 0 }, frame.Memory.Arena.Read(First + 0x69, 1));
        Assert.Equal(0u, task.ReadDword(0x34 + 0x2C));
        Assert.Equal(0u, task.ReadDword(0x34 + 0x30));
        Assert.Equal((ushort)0, task.ReadWord(0x34 + 0x14));
        Assert.Equal((ushort)1, frame.State.Counter26D76C0);
    }

    [Fact]
    public void Batched_node_stores_clear_only_observed_bytes_and_preserve_unknown_bytes_outside_the_allocation()
    {
        var fixture = Fixture();
        int unknownTail = Second + 0x16F80 - 1;
        NativeBattleEffectArena arena = fixture.Frame.Memory.Arena!.MarkUnknown(First + 0x10, 4).MarkUnknown(unknownTail, 1);
        NativeBattleCuragaFrame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = arena } };
        NativeBattleCuragaNodeAllocation allocated = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Address, 0, 5, 0);
        Assert.Equal(new byte[4], allocated.Frame.Memory.Arena!.Read(First + 0x10, 4));
        Assert.Throws<NotSupportedException>(() => allocated.Frame.Memory.Arena.Read(unknownTail, 1));
        NativeBattleCuragaFrame unknownFlag = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = fixture.Frame.Memory.Arena!.MarkUnknown(First + 0x69, 1) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaNodes.AllocateEmitter881700(unknownFlag, fixture.Address, 0, 5, 0));
    }

    private static uint Dword(NativeBattleCuragaFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(offset, 4));

    private static (NativeBattleCuragaFrame Frame, uint Address) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 23, 28, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(28, 27, new(28, 0x87DB10, 0x87DAF0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x26D75BC, 0)] };
        NativeBattleCuragaFrame frame = NativeBattleCuragaController.Initialize87DB10(NativeBattleEffectMemory.FromColdProcess().BeginLoad(27), selected, packets, new(new("skipped-registration", []), 0x4946F));
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(3, 0x87E1A0, 676, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Pools }, State = frame.State with { Auxiliary25F8 = First, Auxiliary16F80 = Second, Counter26D76C0 = 0, Counter26D82E8 = 0 } };
        return (frame, child.Address);
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
