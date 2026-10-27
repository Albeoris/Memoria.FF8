using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectricDischarge126StateTests
{
    [Fact]
    public void Loader_globals_and_PE_mutable_seed_have_distinct_knownness()
    {
        NativeBattleElectricDischarge126State state = NativeBattleElectricDischarge126State.FromColdProcess();
        Assert.Equal(0u, state.ReadDword(0x2521360));
        Assert.True(state.ColdSourcesPending);
        Assert.Throws<NotSupportedException>(() => state.ReadFlashThreshold());
        NativeBattleElectricDischarge126State unknown = NativeBattleElectricDischarge126State.FromUnknown();
        Assert.Null(unknown.ColdSourcesPending);
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(0x2521360));
        Assert.Throws<NotSupportedException>(() => unknown.ReadFlashThreshold());
    }

    [Theory]
    [InlineData(0x25212D4u)]
    [InlineData(0x25212D8u)]
    [InlineData(0x25212E8u)]
    [InlineData(0x25212FCu)]
    [InlineData(0x2521300u)]
    [InlineData(0x2521310u)]
    [InlineData(0x2521320u)]
    [InlineData(0x2521330u)]
    [InlineData(0x2521340u)]
    [InlineData(0x2521350u)]
    public void Original_task_bank_and_descriptors_are_not_duplicate_globals(uint address)
    {
        NativeBattleElectricDischarge126State state = NativeBattleElectricDischarge126State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }

    [Fact]
    public void Warm_actor_colors_threshold_and_unknown_scratch_are_standalone_state()
    {
        NativeBattleElectricDischarge126State state = NativeBattleElectricDischarge126State.FromUnknown().WriteDword(0x25212EC, 0x12ABCDEF).WriteDword(0x25212F8, 0x20EBF2C) with { FlashThreshold = 0x7000 };
        state.Validate();
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state));
        NativeBattleElectricDischarge126State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleElectricDischarge126State>(json)!);
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal(0x7000u, restored.ReadFlashThreshold());
        Assert.Equal(0x12ABCDEFu, restored.ReadDword(0x25212EC));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(0x2521360));
    }
}
