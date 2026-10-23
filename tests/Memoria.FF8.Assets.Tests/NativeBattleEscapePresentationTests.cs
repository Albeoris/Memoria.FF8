using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEscapePresentationTests
{
    [Fact]
    public void Original_non_boss_entry_escape_message_and_fade_resume_from_independent_json()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(512);
        int model = formation.Enemies.First().ModelId;
        BattleSession seed = NativeBattleGraphicsEntryTests.Create(game, assets, model, formation: formation);
        BattleDefinition definition = seed.Capture().Definition with
        {
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeMessageSpeed = game.ReadNewGameInputConfiguration().BattleMessageSpeed,
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte,
            NativeStatusRules = assets.ReadNativeStatusRules(game.ReadNewGameInputConfiguration().NativeBattleSpeedByte!.Value),
            PersistentAiVariables = new uint[8],
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            Combatants = seed.Capture().Definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { NativeTemplate = assets.ReadEnemyTemplate(model, 1), NativeAi = assets.ReadEnemyAi(model, actor.Level) }).ToArray()
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 1200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.False(battle.EscapeDisabled);
        BattleSnapshot ready = battle.Capture();
        battle = BattleSession.Restore(ready with { Random = NativeBattleRandomState.ForecastPhase(1) });
        battle.PublishNativeEscapeButtons(3);
        for (int frame = 0; frame < 30; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(30, battle.Escape.HeldFrames);
        BattleSession restored = RoundTrip(battle);
        for (int frame = 0; frame < 30; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal((byte?)2, battle.NativeResult);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        restored = RoundTrip(battle);
        for (int callback = 0; callback < 1200 && battle.Outcome == BattleOutcome.Active; callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(BattleOutcome.Escaped, battle.Outcome);
        Assert.True(battle.CanReturnToField);
        Assert.Equal((byte?)0, battle.NativeGraphics!.NativeOutcome!.FadeDurationB0C);
        Assert.Equal((byte?)2, battle.NativeResult);
        Assert.Contains(battle.Events, item => item.Kind == "NativeText" && item.Message == definition.NativeGlobalMessages[1]);
        Assert.Single(battle.Events, item => item.Kind == "Outcome");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }
}
