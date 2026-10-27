using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMorph172Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMorph172StateTests
{
    [Fact]
    public void Cold_loader_and_unknown_import_distinguish_mutable_vertices_radii_packets_and_task_owners()
    {
        NativeBattleMorph172State cold = NativeBattleMorph172State.FromColdProcess();
        NativeBattleMorph172State unknown = NativeBattleMorph172State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(new byte[MutableVerticesLength], cold.Read(MutableVerticesA, MutableVerticesLength));
        Assert.Equal(new byte[RadiiLength], cold.Read(RadiiBase, RadiiLength));
        Assert.Equal(new byte[4], cold.ReadPacket(PacketBase + PacketBankBytes, 4));
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(MutableVerticesB + MutableVerticesLength - 2));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(RadiiBase));
        Assert.Throws<NotSupportedException>(() => unknown.ReadPacket(PacketBase, 4));
        Assert.Throws<NotSupportedException>(() => unknown.RootBankStorage!.Read(0, 2));
    }

    [Fact]
    public void Partial_vertex_XYZ_publication_preserves_W_and_other_physical_fronts()
    {
        NativeBattleMorph172State state = NativeBattleMorph172State.FromUnknown().WriteWord(MutableVerticesA + 6, 0x9876).WriteWord(MutableVerticesB + 6, 0x1234).WriteDword(RadiiBase, 0x456789AB);
        NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 2, 3, 4, 5, 6 });
        state = state.WriteView(MutableVerticesA, xyz, 6);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, state.Read(MutableVerticesA, 6));
        Assert.Equal((ushort)0x9876, state.ReadWord(MutableVerticesA + 6));
        Assert.Equal((ushort)0x1234, state.ReadWord(MutableVerticesB + 6));
        Assert.Equal(0x456789ABu, state.ReadDword(RadiiBase));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MutableVerticesA + 14));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MutableVerticesB));
    }

    [Fact]
    public void Particle_head_clear_retains_payload_and_private_packets_through_JSON()
    {
        NativeBattleMorph172State state = NativeBattleMorph172State.FromUnknown().WriteWord(ParticleBase + 22, 0xABCD).WriteDword(PacketBase + PacketBankBytes, 0xAABBCCDD).WriteDword(CursorGlobal, PacketBase + PacketBankBytes + 16);
        for (uint row = 0; row < ParticleCount; row++)
        {
            state = state.WriteDword(ParticleBase + row * ParticleStride, 0);
        }
        NativeBattleMorph172State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleMorph172State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal((ushort)0xABCD, restored.ReadWord(ParticleBase + 22));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(ParticleBase + 4));
        Assert.Equal(0xAABBCCDDu, restored.ReadDword(PacketBase + PacketBankBytes));
        Assert.Equal(PacketBase + PacketBankBytes + 16, restored.ReadDword(CursorGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Sparse_owner_excludes_descriptors_tasks_scalar_gap_and_TIM_COMMON()
    {
        NativeBattleMorph172State state = NativeBattleMorph172State.FromColdProcess();
        foreach (uint address in new[] { RootBank, RootDescriptor, ChildBank, ChildDescriptor, ParametersGlobal + 8, InitializerGlobal - 4, RadiiBase + RadiiLength, 0x20DFAB8u })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => state.ReadPacket(CursorGlobal, 4));
        Assert.Equal(MutableVerticesA, ParticleBase + ParticleBankBytes);
        Assert.Equal(RadiiBase, MutableVerticesA + MutableVerticesLength);
        Assert.Equal(ChildBank, MutableVerticesB + MutableVerticesLength);
        Assert.Equal(ParametersGlobal, ChildBank + ChildBankLength);
    }

    [Fact]
    public void Active_task_banks_transfer_exclusively_and_private_packet_owner_is_bounded()
    {
        NativeBattleMorph172State state = NativeBattleMorph172State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Packets = state.Packets.Write(PacketBytes, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(ParametersGlobal + 8 - BaseAddress)), new byte[1]) }).Validate());
    }
}
