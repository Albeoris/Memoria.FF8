using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect287StateTests
{
    [Fact]
    public void Process_factory_initializes_exact_scalar_root_and_inactive_mesh_queue_owners()
    {
        NativeBattleEffect287State state = Assert.IsType<NativeBattleEffect287State>(NativeBattleEffectMemory.FromColdProcess().Effect287);
        state.Validate();
        Assert.Equal(2, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        Assert.Equal(new byte[8], state.Read(MeshQueueAddress, 8));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(MeshQueueAddress + 8, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(MainDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8 + MorphOffset, MorphLength));
    }

    [Fact]
    public void Unknown_scalar_bytes_and_warm_random_and_cursor_survive_JSON()
    {
        NativeBattleEffect287State state = NativeBattleEffect287State.FromUnknown().WriteWord(CasterGlobal, 4).WriteDword(CursorGlobal, 123).WriteDword(RandomGlobal, 456);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect287State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal((ushort)4, state.ReadWord(CasterGlobal));
        Assert.Equal(123u, state.ReadDword(CursorGlobal));
        Assert.Equal(456u, state.ReadDword(RandomGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ArenaGlobal));
        Assert.Throws<InvalidDataException>(() => state.Read(CasterGlobal + 4, 4));
    }

    [Fact]
    public void Physical_root_transfer_preserves_partial_current_payload()
    {
        NativeBattleEffect287State state = NativeBattleEffect287State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xABCD));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect287State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal(BitConverter.GetBytes((ushort)0xABCD), state.ReadBank(0).Read(14, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(16, 4));
    }

    [Fact]
    public void Mesh_queue_exclusivity_preserves_actual_empty_heads_and_unknown_tail()
    {
        NativeBattleEffect287State state = NativeBattleEffect287State.FromUnknown().WriteDword(MeshQueueAddress, 0);
        NativeBattleEffectArena queue = state.ReadMeshQueue();
        Assert.Equal(0u, state.ReadDword(MeshQueueAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(MeshQueueAddress + 4));
        state = state.DetachMeshQueue();
        Assert.Null(state.MeshQueueStorage);
        Assert.Throws<InvalidDataException>(() => state.ReadMeshQueue());
        Assert.Throws<InvalidDataException>(() => state.Read(MeshQueueAddress, 4));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect287State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state = state.AttachMeshQueue(queue);
        Assert.Throws<InvalidDataException>(() => state.AttachMeshQueue(queue));
        Assert.Equal(0u, state.ReadDword(MeshQueueAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(MeshQueueAddress + 4));
        state.Validate();
    }
}
