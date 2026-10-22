using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedBackColorTests
{
    [Fact]
    public void Legacy_three_control_values_keep_their_JSON_and_are_all_known()
    {
        const string json = "{\"Red\":-16,\"Green\":32,\"Blue\":48}";
        NativeBattleScriptedBackColor back = JsonSerializer.Deserialize<NativeBattleScriptedBackColor>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(back));
        Assert.Same(back, back.RequireKnown());
        Assert.Equal(-16, back.ReadLane(0));
        Assert.Equal(32, back.ReadLane(1));
        Assert.Equal(48, back.ReadLane(2));
    }

    [Fact]
    public void Partial_control_write_preserves_unknown_lanes_across_JSON_without_creating_values()
    {
        NativeBattleScriptedBackColor partial = NativeBattleScriptedBackColor.Unknown().WriteLane(0, -1);
        Assert.Equal(-1, partial.ReadLane(0));
        Assert.Null(partial.ReadLane(1));
        Assert.Null(partial.ReadLane(2));
        Assert.Throws<NotSupportedException>(() => partial.RequireKnown());
        NativeBattleScriptedBackColor restored = JsonSerializer.Deserialize<NativeBattleScriptedBackColor>(JsonSerializer.Serialize(partial))!;
        Assert.Equal(-1, restored.ReadLane(0));
        Assert.Null(restored.ReadLane(1));
        Assert.Null(restored.ReadLane(2));
        NativeBattleScriptedBackColor complete = restored.WriteLane(1, 99).WriteLane(2, -3000);
        Assert.Same(complete, complete.RequireKnown());
        Assert.Equal(99, complete.ReadLane(1));
        Assert.Equal(-3000, complete.ReadLane(2));
        Assert.Null(partial.ReadLane(1));
    }

    [Fact]
    public void Partial_write_preserves_actual_other_controls_and_rejects_invalid_masks()
    {
        NativeBattleScriptedBackColor back = new(11, 22, 33);
        NativeBattleScriptedBackColor updated = back.WriteLane(0, -7);
        Assert.Equal(-7, updated.ReadLane(0));
        Assert.Equal(22, updated.ReadLane(1));
        Assert.Equal(33, updated.ReadLane(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => updated.ReadLane(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => updated.WriteLane(-1, 0));
        NativeBattleScriptedBackColor malformed = back with { Known = [true] };
        Assert.Throws<InvalidDataException>(() => new NativeBattleScriptedLightingMemory(null, null, malformed).Validate());
    }
}
