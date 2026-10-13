using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeScriptBattleFlagTests
{
    [Fact]
    public void New_game_mask20_clear_survives_proved_other_flag_updates()
    {
        NativeWorldControlState state = NativeWorldControlState.CreateNewGame();
        Assert.False(state.ScriptBattleDisabled20);
        Assert.False(state.WithPartyPower(0).WithNormalFieldLoad().WithWorldEntryMenuFlag().ScriptBattleDisabled20);
    }

    [Fact]
    public void Omitted_historical_bit_stays_unknown_during_restore_and_normal_load()
    {
        JsonNode original = JsonNode.Parse(JsonSerializer.Serialize(NativeWorldControlState.CreateNewGame()))!;
        original.AsObject().Remove(nameof(NativeWorldControlState.ScriptBattleDisabled20));
        NativeWorldControlState restored = original.Deserialize<NativeWorldControlState>()!;
        restored.Validate();
        Assert.Null(restored.ScriptBattleDisabled20);
        Assert.Null(restored.WithNormalFieldLoad().WithPartyPower(1).ScriptBattleDisabled20);
        Assert.Null(NativeWorldControlState.CreateVerifiedLegacyNewGame(NativeWorldControlState.LegacyCheckpoint172PreinitSha256).ScriptBattleDisabled20);
    }
}
