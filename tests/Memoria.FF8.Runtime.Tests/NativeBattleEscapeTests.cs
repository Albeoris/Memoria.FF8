using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEscapeTests
{
    [Theory]
    [InlineData(0, 0, 1, true)]
    [InlineData(1, 0, 1, false)]
    [InlineData(4, 0, 1, false)]
    [InlineData(2, 0, 1, true)]
    [InlineData(0, 1, 1, false)]
    [InlineData(0, 8, 1, false)]
    [InlineData(0, 0x4000, 1, false)]
    [InlineData(0, 0x10, 1, true)]
    [InlineData(0, 0, 0, false)]
    [InlineData(0, 0, 0x4001, false)]
    public void Participant_gate_uses_each_native_status_and_active_flag(ushort saved, uint battle, uint flags, bool expected)
    {
        Assert.Equal(expected, NativeBattleEscapeRules.Eligible(saved, battle, flags));
    }

    [Theory]
    [InlineData(0, 0, 64)]
    [InlineData(0, 8, 128)]
    [InlineData(0, 16, 16)]
    [InlineData(0, 24, 16)]
    [InlineData(1, 0, 16)]
    [InlineData(2, 8, 16)]
    [InlineData(3, 16, 255)]
    [InlineData(4, 24, 255)]
    public void Threshold_preserves_advantage_and_enemy_flag_precedence(byte advantage, byte information, byte expected)
    {
        Assert.Equal(expected, NativeBattleEscapeRules.Threshold(advantage, [0, information]));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(255)]
    public void Random_comparison_includes_the_threshold(byte threshold)
    {
        Assert.True(NativeBattleEscapeRules.Succeeds((byte)(threshold - 1), threshold));
        Assert.True(NativeBattleEscapeRules.Succeeds(threshold, threshold));
        if (threshold < 255)
        {
            Assert.False(NativeBattleEscapeRules.Succeeds((byte)(threshold + 1), threshold));
        }
        Assert.Equal((byte)255, NativeBattleEscapeRules.Threshold(0, []));
    }

    [Fact]
    public void Sixty_consecutive_held_frames_escape_and_json_restoration_preserves_the_midpoint()
    {
        BattleSession battle = Create();
        Assert.NotEqual(0u, battle.Combatants[0].NativeCombatantFlags & 1);
        battle.PublishNativeEscapeButtons(3);
        Tick(battle, 30);
        Assert.Equal(30, battle.Escape.HeldFrames);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(battle.Capture())))!);
        Tick(battle, 29);
        Tick(restored, 29);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(59, restored.Escape.HeldFrames);
        battle.Tick();
        restored.Tick();
        Assert.Equal(BattleOutcome.Escaped, battle.Outcome);
        Assert.True(battle.Escape.Completed);
        Assert.Equal((byte?)2, battle.NativeResult);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(battle.Capture())), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored.Capture())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Release_or_either_button_alone_resets_the_consecutive_hold(ushort buttons)
    {
        BattleSession battle = Create();
        battle.PublishNativeEscapeButtons(3);
        Tick(battle, 59);
        battle.PublishNativeEscapeButtons(buttons);
        Tick(battle, 61);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(0, battle.Escape.HeldFrames);
        Assert.False(battle.Escape.Requested);
        battle.PublishNativeEscapeButtons(3);
        Tick(battle, 59);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        battle.Tick();
        Assert.Equal(BattleOutcome.Escaped, battle.Outcome);
    }

    [Theory]
    [InlineData(0, null, false)]
    [InlineData(1, null, true)]
    [InlineData(1, false, false)]
    [InlineData(0, true, true)]
    public void Effective_no_escape_flag_honors_the_script_override(ushort flags, bool? scriptDisabled, bool expectedDisabled)
    {
        BattleSession battle = Create(flags);
        BattleSnapshot snapshot = battle.Capture();
        battle = BattleSession.Restore(snapshot with { NativeScriptState = snapshot.NativeScriptState with { EscapeDisabled = scriptDisabled } });
        Assert.Equal(expectedDisabled, battle.EscapeDisabled);
        battle.PublishNativeEscapeButtons(3);
        Tick(battle, 60);
        Assert.Equal(expectedDisabled ? BattleOutcome.Active : BattleOutcome.Escaped, battle.Outcome);
    }

    [Fact]
    public void Snapshot_without_escape_state_starts_with_no_buttons_held()
    {
        JsonObject snapshot = JsonSerializer.SerializeToNode(Create().Capture())!.AsObject();
        Assert.True(snapshot.Remove("Escape"));
        BattleSession restored = BattleSession.Restore(snapshot.Deserialize<BattleSnapshot>()!);
        Assert.Equal(new NativeBattleEscapeState(), restored.Escape);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Ai23_sets_or_clears_the_effective_escape_flag_using_exact_operand_one(byte operand, bool expected)
    {
        BattleDefinition definition = Create(1).Capture().Definition;
        NativeBattleAiDefinition ai = new([[23, operand, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        definition = definition with
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { NativeAi = ai }).ToArray()
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.Tick();
        Assert.Equal(expected, battle.EscapeDisabled);
        Assert.Equal(expected, battle.Capture().NativeScriptState.EscapeDisabled);
        Assert.Equal(expected ? 1 : 0, battle.Capture().NativeScriptState.ResultFlags & 1);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(battle.Capture())))!);
        Assert.Equal(expected, restored.EscapeDisabled);
    }

    private static BattleSession Create(ushort flags = 0)
    {
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 1, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        return new(new BattleDefinition
        {
            NativeFlags = flags,
            InitialRandom = NativeBattleRandomState.ForecastPhase(1),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, Speed = 1, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 0 } }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static void Tick(BattleSession battle, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            battle.Tick();
        }
    }
}
