using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSavedFlagOutcomeTests
{
    [Fact]
    public void Factual_opcode54_receipt_checks_the_current_byte_and_commits_once_with_the_battle_result()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().Rest();
        NativeSavedBattleFlags originalFlags = new(NativeInputConfiguration.OriginalInitHash, 0xF6);
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance(), Economy = assets.ReadNewGameEconomy(), SavedBattleFlags = originalFlags, BattleVariables = NativeBattleVariableState.CreateNewGame() });
        using GameWorld world = new(session, assets.GameDirectory);
        BattleDefinition installed = world.CreateEncounterBattle(512);
        // Isolated component combat: original reward sources, short HP/ATB inputs and an explicit opcode54 callback.
        BattleDefinition definition = installed with { Combatants = installed.Combatants.Select(actor => actor.IsPlayer ? actor with { Attack = 255, Speed = 10_000, InitialAtb = 10_000 } : actor with { CurrentHp = 1, Speed = 1, NativeAi = actor.NativeAi! with { Scripts = [[54, 0], [0], [0], [0], [0]] } }).ToArray() };
        BattleSession active = new(definition, BattleAtbMode.Normal);
        for (int frame = 0; frame < 1000 && active.Outcome == BattleOutcome.Active; frame++)
        {
            BattleCombatant? actor = active.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady);
            BattleCombatant? target = active.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.IsAlive);
            if (actor is not null && target is not null)
            {
                active.QueueAttack(actor.Id, target.Id);
            }
            active.Tick();
        }
        Assert.Equal(BattleOutcome.Victory, active.Outcome);
        BattleEncounterOccurrence occurrence = new("saved-flag-component", 512, world.BattleResources.FormationSourceHash(512), SceneKind.Field, 999, 0, new string('A', 64), null);
        session.SetPendingBattleEncounter(occurrence);
        BattleResult result = world.CaptureBattleOutcome(active, occurrence);
        string before = JsonSerializer.Serialize(session.State);
        BattleResult wrongBefore = result with { NativeScriptEffects = result.NativeScriptEffects with { SavedBattleFlagsBefore = 0xF4 } };

        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(occurrence, wrongBefore));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        world.ApplyBattleResult(occurrence, result);

        Assert.Equal((byte)0xFC, session.State.SavedBattleFlags!.Value);
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(occurrence, result));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
    }
}
