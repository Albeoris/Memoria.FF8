using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleChainGun190Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChainGun190StateTests
{
    [Fact]
    public void Cold_loader_and_unknown_import_distinguish_records_particles_private_packets_and_task_owners()
    {
        NativeBattleChainGun190State cold = NativeBattleChainGun190State.FromColdProcess();
        NativeBattleChainGun190State unknown = NativeBattleChainGun190State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Equal(new byte[4], cold.Read(PacketBase + PacketBankBytes, 4));
        Assert.Equal(new byte[RecordsLength], cold.Read(RecordsAddress, RecordsLength));
        Assert.Equal(new byte[ParticleStride], cold.Read(ParticleABase, ParticleStride));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadPacket(PacketBase, 4));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(RecordsAddress + RecordsLength - 2));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(ParticleBBase + 4));
        Assert.Throws<NotSupportedException>(() => unknown.ChildBankStorage!.Read(0, 4));
    }

    [Fact]
    public void Native_particle_and_48_record_head_clears_preserve_payload_and_unknown_suffixes()
    {
        NativeBattleChainGun190State state = NativeBattleChainGun190State.FromUnknown().WriteDword(ParticleABase + 8, 0x12345678).WriteWord(ParticleBBase + 22, 0x9876).WriteDword(RecordsAddress + RecordsLength - 12, 0xFFFFFFFF).WriteWord(RecordsAddress + RecordsLength - 2, 0xBEEF);
        for (uint index = 0; index < ParticleCount; index++)
        {
            state = state.WriteDword(ParticleABase + index * ParticleStride, 0).WriteDword(ParticleBBase + index * ParticleStride, 0);
        }
        for (uint index = 0; index < RecordCount; index++)
        {
            state = state.WriteDword(RecordsAddress + index * RecordStride, 0);
        }
        Assert.Equal(0u, state.ReadDword(RecordsAddress + RecordsLength - RecordStride));
        Assert.Equal(uint.MaxValue, state.ReadDword(RecordsAddress + RecordsLength - 12));
        Assert.Equal((ushort)0xBEEF, state.ReadWord(RecordsAddress + RecordsLength - 2));
        Assert.Equal(0x12345678u, state.ReadDword(ParticleABase + 8));
        Assert.Equal((ushort)0x9876, state.ReadWord(ParticleBBase + 22));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RecordsAddress + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ParticleABase + 4));
    }

    [Fact]
    public void Interpolated_record_XYZ_retains_W_and_private_packet_masks_survive_JSON()
    {
        NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 2, 3, 4, 5, 6 });
        uint last = RecordsAddress + (RecordCount - 1) * RecordStride;
        NativeBattleChainGun190State state = NativeBattleChainGun190State.FromUnknown().WriteWord(last + 14, 0xABCD).WriteDword(last, 1).WriteDword(last + 4, uint.MaxValue).WriteView(last + 8, xyz, 6).WriteView(PacketBase, xyz, 8).WriteBytes(PacketBase + PacketBankBytes, new byte[] { 9 }).WriteDword(CursorGlobal, PacketBase + 8);
        NativeBattleChainGun190State restored = JsonSerializer.Deserialize<NativeBattleChainGun190State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, restored.ReadBytes(last + 8, 6));
        Assert.Equal((ushort)0xABCD, restored.ReadWord(last + 14));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, restored.ReadPacket(PacketBase, 6));
        Assert.Equal(new byte[] { 9 }, restored.ReadPacket(PacketBase + PacketBankBytes, 1));
        Assert.Throws<NotSupportedException>(() => restored.ReadPacket(PacketBase + 6, 2));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(RecordsAddress + 14));
        Assert.Equal(PacketBase + 8, restored.ReadDword(CursorGlobal));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Sparse_globals_exclude_descriptors_gaps_task_banks_COMMON_and_packet_owner_crossings()
    {
        NativeBattleChainGun190State state = NativeBattleChainGun190State.FromColdProcess();
        foreach (uint address in new[] { RootBank, RootDescriptor, ChildBank, ChildDescriptor, FirstTargetGlobal + 4, ParticleBBase + ParticleBankBytes, InitializerGlobal + 4, 0x20DFAB8u })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(PacketBase - 1, 2));
        Assert.Throws<InvalidDataException>(() => state.Read(CursorGlobal - 1, 2));
        Assert.Throws<InvalidDataException>(() => state.ReadPacket(CursorGlobal, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadPacket(RecordsAddress, 4));
        Assert.Equal(ParticleBBase, ParticleABase + ParticleBankBytes);
        Assert.Equal(ChildDescriptor, RecordsAddress + RecordsLength);
        Assert.Equal(ParametersGlobal, ChildBank + ChildBankLength);
        Assert.Equal(CursorGlobal, PacketBase + PacketBytes);
    }

    [Fact]
    public void Active_task_transfer_revokes_inactive_owners_and_all_retained_storage_is_bounded()
    {
        NativeBattleChainGun190State state = NativeBattleChainGun190State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Packets = state.Packets.Write(PacketBytes, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(PacketBase - BaseAddress)), new byte[1]) }).Validate());
    }
}
