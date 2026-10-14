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
    public void Omitted_historical_bit_requires_recreation()
    {
        JsonNode original = JsonNode.Parse(JsonSerializer.Serialize(NativeWorldControlState.CreateNewGame()))!;
        original.AsObject().Remove(nameof(NativeWorldControlState.ScriptBattleDisabled20));
        Assert.Throws<JsonException>(() => original.Deserialize<NativeWorldControlState>());
        NativeWorldControlState unknown = NativeWorldControlState.CreateNewGame() with { ScriptBattleDisabled20 = null };
        Assert.Throws<InvalidDataException>(unknown.Validate);
        Assert.Throws<InvalidDataException>(() => unknown.WithNormalFieldLoad());
    }
}
