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
    public void Missing_current_event_word_requires_recreation()
    {
        JsonNode original = JsonNode.Parse(JsonSerializer.Serialize(NativeWorldControlState.CreateNewGame()))!;
        original.AsObject().Remove(nameof(NativeWorldControlState.EventFlags104));
        Assert.Throws<JsonException>(() => original.Deserialize<NativeWorldControlState>());
        Assert.DoesNotContain(nameof(NativeWorldControlState.ScriptBattleDisabled20), JsonSerializer.Serialize(NativeWorldControlState.CreateNewGame()));
    }
}
