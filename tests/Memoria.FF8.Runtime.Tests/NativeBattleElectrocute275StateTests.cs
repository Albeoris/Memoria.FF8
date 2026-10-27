using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectrocute275StateTests
{
    [Fact]
    public void Loader_globals_and_PE_mutable_seed_have_distinct_knownness()
    {
        NativeBattleElectrocute275State state = NativeBattleElectrocute275State.FromColdProcess();
        Assert.Equal(0u, state.ReadDword(0x2508408));
        Assert.True(state.ColdSourcesPending);
        Assert.Throws<NotSupportedException>(() => state.ReadFlashThreshold());
        NativeBattleElectrocute275State unknown = NativeBattleElectrocute275State.FromUnknown();
        Assert.False(unknown.ColdSourcesPending);
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(0x2508408));
        Assert.Throws<NotSupportedException>(() => unknown.ReadFlashThreshold());
    }

    [Theory]
    [InlineData(0x2508360u)]
    [InlineData(0x2508370u)]
    [InlineData(0x25083A8u)]
    [InlineData(0x25083B8u)]
    [InlineData(0x25083C8u)]
    [InlineData(0x25083D8u)]
    [InlineData(0x25083E8u)]
    [InlineData(0x25083F8u)]
    public void Original_task_bank_and_descriptors_are_not_duplicate_globals(uint address)
    {
        NativeBattleElectrocute275State state = NativeBattleElectrocute275State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }

    [Fact]
    public void Warm_actor_colors_threshold_and_unknown_scratch_are_standalone_state()
    {
        NativeBattleElectrocute275State state = NativeBattleElectrocute275State.FromUnknown().WriteDword(0x2508374, 0x12ABCDEF).WriteDword(0x25083A4, 0x20EBF2C) with { FlashThreshold = 0x7000 };
        state.Validate();
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state));
        NativeBattleElectrocute275State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleElectrocute275State>(json)!);
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal(0x7000u, restored.ReadFlashThreshold());
        Assert.Equal(0x12ABCDEFu, restored.ReadDword(0x2508374));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(0x2508408));
    }
}
