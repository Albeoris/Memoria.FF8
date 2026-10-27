using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleCannonBlow151Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCannonBlow151StateTests
{
    [Fact]
    public void Loader_zero_and_unknown_import_distinguish_scalar_curve_particle_and_task_owners()
    {
        NativeBattleCannonBlow151State cold = NativeBattleCannonBlow151State.FromColdProcess();
        NativeBattleCannonBlow151State unknown = NativeBattleCannonBlow151State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(WorkingGlobal));
        Assert.Equal(new byte[SamplesLength], cold.Read(SamplesAddress, SamplesLength));
        Assert.Equal(new byte[ParticleStride], cold.Read(ParticleABase, ParticleStride));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(WorkingGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(SamplesAddress + SamplesLength - 2));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(ParticleBBase + 4));
        Assert.Throws<NotSupportedException>(() => unknown.ChildBankStorage!.Read(0, 4));
    }

    [Fact]
    public void Particle_flag_clear_preserves_warm_payload_and_unknown_padding()
    {
        NativeBattleCannonBlow151State state = NativeBattleCannonBlow151State.FromUnknown().WriteDword(ParticleABase + 8, 0x12345678).WriteWord(ParticleBBase + 22, 0x9876);
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
    public void Full_curve_extent_retains_XYZ_only_store_masks_and_warm_W_through_JSON()
    {
        NativeBattleCannonBlow151State state = NativeBattleCannonBlow151State.FromUnknown();
        for (uint sample = 0; sample < SampleCount; sample++)
        {
            state = state.WriteWord(SamplesAddress + sample * SampleStride + 6, (ushort)(0xA000 + sample));
        }
        NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 2, 3, 4, 5, 6 });
        uint last = SamplesAddress + (SampleCount - 1) * SampleStride;
        state = state.WriteView(last, xyz, 6).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + PacketStep);
        NativeBattleCannonBlow151State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCannonBlow151State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, restored.ReadView(last, 8).Read(0, 6));
        Assert.Equal((ushort)0xA01F, restored.ReadWord(last + 6));
        Assert.Equal(ArenaAddress, restored.ReadDword(WorkingGlobal));
        Assert.Equal(ArenaAddress + PacketStep, restored.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(SamplesAddress));
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Selected_lane_zero_excludes_other_lanes_gaps_task_banks_and_COMMON_bytes()
    {
        NativeBattleCannonBlow151State state = NativeBattleCannonBlow151State.FromColdProcess();
        foreach (uint address in new[] { RootBank, RootDescriptor, ChildBank, ChildDescriptor, ControlPointsAddress + 0x20, TargetPointAddress + 8, CurveMatrixAddress + 0x20, CasterPointGlobal + 8, SamplesAddress + SamplesLength, ArenaAddress, ParticleBBase + ParticleBankBytes })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Equal(ParticleABase + ParticleBankBytes, ParticleBBase);
        Assert.Equal(ChildBank + ChildBankLength, ControlPointsAddress);
        Assert.Equal(SampleCount * SampleStride, SamplesLength);
    }

    [Fact]
    public void Transferred_task_banks_remove_inactive_owners_and_retained_bytes_remain_bounded()
    {
        NativeBattleCannonBlow151State state = NativeBattleCannonBlow151State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(ChildBank - BaseAddress)), new byte[1]) }).Validate());
    }
}
