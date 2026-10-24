using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect76StateTests
{
    [Fact]
    public void Cold_state_owns_only_selected_sparse_bytes_and_inactive_banks()
    {
        NativeBattleEffect76State state = NativeBattleEffect76State.FromColdProcess();
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(0x24C327C, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(MusicCompletionGlobal + 1, 7));
        Assert.Throws<InvalidDataException>(() => state.Read(0x24C665C, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x1D9A828, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Marker_only_record_writes_do_not_initialize_retained_payload_or_matrix_padding()
    {
        NativeBattleEffect76State state = NativeBattleEffect76State.FromUnknown();
        state = state.WriteDword(FirstRecordsAddress, 0).WriteDword(SecondRecordsAddress, 0).WriteWord(ViewAddress, 4096);
        NativeBattleEffect76State restored = JsonSerializer.Deserialize<NativeBattleEffect76State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(0u, restored.ReadDword(FirstRecordsAddress));
        Assert.Equal(0u, restored.ReadDword(SecondRecordsAddress));
        Assert.Equal((ushort)4096, restored.ReadWord(ViewAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(FirstRecordsAddress + 4));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(ViewAddress + 18));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(SavedPointAddress));
    }

    [Fact]
    public void Bank_transfer_is_exclusive_and_retains_unknown_and_warm_task_bytes_after_JSON()
    {
        NativeBattleEffect76State state = NativeBattleEffect76State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectArena bank = state.ReadBank(index).Write(12, BitConverter.GetBytes((ushort)0xCAFE));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(Pools[index].Bank, bank);
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
        }
        state = JsonSerializer.Deserialize<NativeBattleEffect76State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(index).Read(12, 2));
            Assert.Throws<NotSupportedException>(() => state.ReadBank(index).Read(14, 2));
        }
    }

    [Fact]
    public void Camera_tasks_retain_process_and_program_only_at_real_child_slot_addresses()
    {
        NativeBattleGuardianCameraTask camera = new(NativeBattleGuardianCameraProgram.FromColdProcess(), new byte[CameraLength].ToImmutableArray(), 2, 17, false);
        NativeBattleEffect76State state = NativeBattleEffect76State.FromColdProcess() with { CameraTasks = ImmutableDictionary<uint, NativeBattleGuardianCameraTask>.Empty.Add(ChildBank + ChildStride, camera) };
        NativeBattleEffect76State restored = JsonSerializer.Deserialize<NativeBattleEffect76State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal((ushort)17, restored.CameraTasks[ChildBank + ChildStride].Frame);
        Assert.Equal(2, restored.CameraTasks[ChildBank + ChildStride].Offset);
        Assert.Throws<InvalidDataException>(() => (restored with { CameraTasks = restored.CameraTasks.Add(ChildBank + 1, camera) }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { CameraTasks = restored.CameraTasks.SetItem(ChildBank + ChildStride, camera with { Program = [] }) }).Validate());
    }
}
