using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHypnotizeBeamStateTests
{
    [Fact]
    public void Hypnotize_cold_unknown_and_partial_live_JSON_preserve_scalar_knowledge()
    {
        NativeBattleHypnotize133State cold = NativeBattleHypnotize133State.FromColdProcess();
        NativeBattleHypnotize133State unknown = NativeBattleHypnotize133State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(NativeBattleHypnotize133Profile.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(NativeBattleHypnotize133Profile.WorkingGlobal));
        NativeBattleHypnotize133State live = unknown.WriteDword(NativeBattleHypnotize133Profile.WorkingGlobal, 0x20E4000).WriteDword(NativeBattleHypnotize133Profile.CursorGlobal, 0x20F4E20);
        NativeBattleHypnotize133State restored = JsonSerializer.Deserialize<NativeBattleHypnotize133State>(JsonSerializer.Serialize(live))!;
        restored.Validate();
        Assert.Equal(0x20E4000u, restored.ReadDword(NativeBattleHypnotize133Profile.WorkingGlobal));
        Assert.Equal(0x20F4E20u, restored.ReadDword(NativeBattleHypnotize133Profile.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(NativeBattleHypnotize133Profile.CasterGlobal));
        Assert.Equal(JsonSerializer.Serialize(live.WriteWord(NativeBattleHypnotize133Profile.CursorGlobal, 0x1234)), JsonSerializer.Serialize(restored.WriteWord(NativeBattleHypnotize133Profile.CursorGlobal, 0x1234)));
    }
    [Fact]
    public void Beam_Laser_saved_flags_partial_width_and_unknown_padding_survive_JSON()
    {
        NativeBattleBeamLaser134State state = NativeBattleBeamLaser134State.FromUnknown().WriteDword(NativeBattleBeamLaser134Profile.SavedCasterFlagsGlobal, 0xABCD1020).WriteWord(NativeBattleBeamLaser134Profile.SavedCasterFlagsGlobal, 8);
        NativeBattleBeamLaser134State restored = JsonSerializer.Deserialize<NativeBattleBeamLaser134State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(0xABCD0008u, restored.ReadDword(NativeBattleBeamLaser134Profile.SavedCasterFlagsGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(0x2520E7C));
        Assert.Equal(0u, NativeBattleBeamLaser134State.FromColdProcess().ReadDword(NativeBattleBeamLaser134Profile.CursorGlobal));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }
    [Theory]
    [InlineData(0x2520EF8u)]
    [InlineData(0x2520F08u)]
    [InlineData(0x2520F10u)]
    [InlineData(0x2520F50u)]
    public void Hypnotize_cannot_duplicate_task_or_descriptor_bytes(uint address)
    {
        NativeBattleHypnotize133State state = NativeBattleHypnotize133State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 0));
    }
    [Theory]
    [InlineData(0x2520E80u)]
    [InlineData(0x2520E90u)]
    [InlineData(0x2520E98u)]
    [InlineData(0x2520ED8u)]
    public void Beam_Laser_cannot_duplicate_task_or_descriptor_bytes(uint address)
    {
        NativeBattleBeamLaser134State state = NativeBattleBeamLaser134State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 0));
    }
    [Fact]
    public void Both_module_states_are_separate_nullable_JSON_owners_without_arena_alias()
    {
        NativeBattleEffectMemory memory = new(null, 0, 0, [], 0, 0, 0, 0)
        {
            Hypnotize133 = NativeBattleHypnotize133State.FromColdProcess().WriteDword(NativeBattleHypnotize133Profile.CursorGlobal, 0x20DFABC),
            BeamLaser134 = NativeBattleBeamLaser134State.FromUnknown()
        };
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Null(restored.Arena);
        Assert.Equal(0x20DFABCu, restored.Hypnotize133!.ReadDword(NativeBattleHypnotize133Profile.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.BeamLaser134!.ReadDword(NativeBattleBeamLaser134Profile.CursorGlobal));
    }
}
