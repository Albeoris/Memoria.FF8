using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGatlingGun150StateTests
{
    [Fact]
    public void Cold_loader_and_unknown_import_preserve_distinct_physical_owner_knownness()
    {
        NativeBattleGatlingGun150State cold = NativeBattleGatlingGun150State.FromColdProcess();
        NativeBattleGatlingGun150State unknown = NativeBattleGatlingGun150State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Equal(new byte[4], cold.Read(PacketBase + PacketBankBytes, 4));
        Assert.Equal(new byte[ParticleStride], cold.Read(ParticleABase, ParticleStride));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.Read(PacketBase, 4));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(ParticleBBase + 4));
        Assert.Throws<NotSupportedException>(() => unknown.ChildBankStorage!.Read(0, 4));
    }

    [Fact]
    public void Native_flag_initialization_preserves_warm_particle_payload_and_unknown_padding()
    {
        NativeBattleGatlingGun150State state = NativeBattleGatlingGun150State.FromUnknown().WriteDword(ParticleABase + 8, 0x12345678).WriteWord(ParticleBBase + 22, 0x9876);
        for (uint index = 0; index < ParticleCount; index++)
        {
            state = state.WriteDword(ParticleABase + index * ParticleStride, 0).WriteDword(ParticleBBase + index * ParticleStride, 0);
        }
        Assert.Equal(0u, state.ReadDword(ParticleABase + (ParticleCount - 1) * ParticleStride));
        Assert.Equal(0u, state.ReadDword(ParticleBBase));
        Assert.Equal(0x12345678u, state.ReadDword(ParticleABase + 8));
        Assert.Equal((ushort)0x9876, state.ReadWord(ParticleBBase + 22));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ParticleABase + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ParticleBBase + 8));
    }

    [Fact]
    public void Sparse_views_keep_each_packet_bank_and_particle_mask_after_JSON_restore()
    {
        NativeBattleEffectArena partial = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(2, new byte[] { 0x12, 0x34 });
        NativeBattleGatlingGun150State state = NativeBattleGatlingGun150State.FromUnknown().WriteDword(CursorGlobal, PacketBase + 8).WriteView(PacketBase, partial, 8).Write(PacketBase + PacketBankBytes, new byte[] { 9 }).WriteView(ParticleBBase + 8, partial, 8);
        NativeBattleGatlingGun150State restored = JsonSerializer.Deserialize<NativeBattleGatlingGun150State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(PacketBase + 8, restored.ReadDword(CursorGlobal));
        Assert.Equal(new byte[] { 0x12, 0x34 }, restored.ReadView(PacketBase, 8).Read(2, 2));
        Assert.Equal(new byte[] { 0x12, 0x34 }, restored.ReadView(ParticleBBase + 8, 8).Read(2, 2));
        Assert.Equal(new byte[] { 9 }, restored.Read(PacketBase + PacketBankBytes, 1));
        Assert.Throws<NotSupportedException>(() => restored.Read(PacketBase, 1));
        Assert.Throws<NotSupportedException>(() => restored.Read(PacketBase + PacketBankBytes + 1, 1));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Scalar_and_particle_ranges_exclude_task_descriptors_unused_gaps_and_packet_overrun()
    {
        NativeBattleGatlingGun150State state = NativeBattleGatlingGun150State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.WriteDword(ChildBank, 0));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(RootBank, 0));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(RootDescriptor, 0));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(ChildDescriptor, 0));
        Assert.Throws<InvalidDataException>(() => state.WriteWord(InitializerGlobal + 4, 0));
        Assert.Throws<InvalidDataException>(() => state.Read(PacketBase - 1, 2));
        Assert.Throws<InvalidDataException>(() => state.Read(CursorGlobal - 1, 2));
        Assert.Throws<InvalidDataException>(() => state.ReadPacket(CursorGlobal, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadPacket(ParticleABase, 4));
        Assert.Equal(ParticleABase + ParticleBankBytes, ParticleBBase);
        Assert.Equal(ParticleBBase + ParticleBankBytes, ChildDescriptor);
        Assert.Equal(ChildBank + ChildBankLength, ParametersGlobal);
        Assert.Equal(PacketBase + PacketBytes, CursorGlobal);
    }

    [Fact]
    public void Active_task_bank_transfer_removes_inactive_owners_and_retained_owners_stay_bounded()
    {
        NativeBattleGatlingGun150State state = NativeBattleGatlingGun150State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Packets = state.Packets.Write(PacketBytes, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(ChildBank - BaseAddress)), new byte[1]) }).Validate());
    }
}
