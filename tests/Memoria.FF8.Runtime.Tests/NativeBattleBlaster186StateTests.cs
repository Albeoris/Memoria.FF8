using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlaster186StateTests
{
    [Fact]
    public void Loader_zero_and_unknown_import_distinguish_every_mutable_frame_matrix_particle_and_task_owner()
    {
        NativeBattleBlaster186State cold = NativeBattleBlaster186State.FromColdProcess();
        NativeBattleBlaster186State unknown = NativeBattleBlaster186State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        foreach (uint address in MutableFrameAddresses)
        {
            Assert.Equal(new byte[VertexFrameBytes], cold.Read(address, VertexFrameBytes));
            Assert.Throws<NotSupportedException>(() => unknown.ReadWord(address));
            Assert.Throws<NotSupportedException>(() => unknown.ReadWord(address + VertexFrameBytes - 2));
        }
        Assert.Equal(new byte[32], cold.Read(MatrixAddress, 32));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(WorkingGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(ParticleBase));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ChildBankStorage!.Read(0, 4));
    }

    [Fact]
    public void Native_particle_flag_clear_keeps_retained_warm_payload_and_unknown_padding()
    {
        NativeBattleBlaster186State state = NativeBattleBlaster186State.FromUnknown().WriteDword(ParticleBase + 8, 0x12345678).WriteWord(ParticleBase + 22, 0x9876);
        for (uint index = 0; index < ParticleCount; index++)
        {
            state = state.WriteDword(ParticleBase + index * ParticleStride, 0);
        }
        Assert.Equal(0u, state.ReadDword(ParticleBase + (ParticleCount - 1) * ParticleStride));
        Assert.Equal(0x12345678u, state.ReadDword(ParticleBase + 8));
        Assert.Equal((ushort)0x9876, state.ReadWord(ParticleBase + 22));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ParticleBase + 4));
    }

    [Fact]
    public void Split_frame_and_matrix_views_retain_independent_masks_through_JSON()
    {
        NativeBattleEffectArena partial = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 2, 3, 4, 5, 6 });
        NativeBattleBlaster186State state = NativeBattleBlaster186State.FromUnknown().WriteView(MutableFrameAddresses[13] + VertexFrameBytes - 8, partial, 8).WriteDword(MatrixAddress, 0x1234).WriteWord(MutableFrameAddresses[14], 0x5678);
        NativeBattleBlaster186State restored = JsonSerializer.Deserialize<NativeBattleBlaster186State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, restored.Read(MutableFrameAddresses[13] + VertexFrameBytes - 8, 6));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(MatrixAddress - 2));
        Assert.Equal(0x1234u, restored.ReadDword(MatrixAddress));
        Assert.Equal((ushort)0x5678, restored.ReadWord(MatrixAddress + 32));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(MatrixAddress + 4));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Physical_ranges_exclude_task_descriptors_gaps_and_both_packet_owners()
    {
        NativeBattleBlaster186State state = NativeBattleBlaster186State.FromColdProcess();
        foreach (uint address in new uint[] { RootBank, RootDescriptor, ChildBank, ChildDescriptor, 0x22B7FAC, 0x22BC104, 0x22BC11C, ArenaAddress, 0x1D8E054 })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Equal(ChildDescriptor, MutableFrameAddresses[7] + VertexFrameBytes);
        Assert.Equal(ChildBank + ChildBankLength, MutableFrameAddresses[8]);
        Assert.Equal(MatrixAddress, MutableFrameAddresses[13] + VertexFrameBytes);
        Assert.Equal(MatrixAddress + 32, MutableFrameAddresses[14]);
        Assert.Equal(TargetPointAddress, MutableFrameAddresses[15] + VertexFrameBytes);
    }

    [Fact]
    public void Active_task_transfer_removes_inactive_aliases_and_retained_banks_stay_bounded()
    {
        NativeBattleBlaster186State state = NativeBattleBlaster186State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(ChildBank - BaseAddress)), new byte[1]) }).Validate());
    }
}
