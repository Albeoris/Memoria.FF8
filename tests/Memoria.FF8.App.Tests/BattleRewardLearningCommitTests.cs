using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleRewardLearningCommitTests
{
    [Fact]
    public void PreviewIsPureAndConfirmationPreservesEarnedApAndLearningWhileSelectingAnotherAbility()
    {
        using Fixture fixture = CreateFixture();
        string before = JsonSerializer.Serialize(fixture.Session.State);
        BattleResultOwners preview = fixture.World.PreviewBattleResultOwners();
        Assert.Equal(JsonSerializer.Serialize(preview), JsonSerializer.Serialize(fixture.World.PreviewBattleResultOwners()));
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
        Assert.Null(fixture.World.LastBattleRewards);
        BattleGuardianForceRewardPresentation growth = Assert.Single(preview.Rewards.GuardianForceGrowth, growth => growth.Id == 0);
        Assert.Contains(fixture.CompletedAbility.Ability, growth.LearnedAbilityIds);
        Assert.True(preview.CharacterVitals.GuardianForces[0].LearnedBank.Has(fixture.CompletedAbility.Ability));
        Assert.Equal(fixture.CompletedAbility.RequiredPoints - 1 + preview.Rewards.GuardianForceAbilityPoints[0], preview.CharacterVitals.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == fixture.CompletedAbility.Ability).EarnedPoints);
        int selected = NextChoice(preview);
        NativeCharacterVitals expected = preview.CharacterVitals.SelectGuardianForceLearning(0, selected);

        fixture.World.ApplyBattleResult(fixture.Result.Encounter, fixture.Result, new Dictionary<int, int> { [0] = selected });

        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(fixture.Session.State.CharacterVitals));
        Assert.Equal(JsonSerializer.Serialize(preview.Economy), JsonSerializer.Serialize(fixture.Session.State.Economy));
        Assert.Equal(JsonSerializer.Serialize(preview.Rewards), JsonSerializer.Serialize(fixture.World.LastBattleRewards));
        Assert.Equal(selected, fixture.Session.State.CharacterVitals!.GuardianForces[0].Learning);
        Assert.Null(fixture.Session.State.PendingBattleEncounter);
        Assert.Equal(fixture.Result.Encounter.OccurrenceId, Assert.Single(fixture.Session.State.AppliedBattleResultIds));
        string committed = JsonSerializer.Serialize(fixture.Session.State);
        BattleRewardSummary? rewards = fixture.World.LastBattleRewards;
        Assert.Throws<InvalidOperationException>(() => fixture.World.ApplyBattleResult(fixture.Result.Encounter, fixture.Result, new Dictionary<int, int> { [0] = selected }));
        Assert.Equal(committed, JsonSerializer.Serialize(fixture.Session.State));
        Assert.Same(rewards, fixture.World.LastBattleRewards);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void InvalidOrUncompletedGuardianForceChoiceRejectsTheEntireCommit(int guardianForce)
    {
        using Fixture fixture = CreateFixture();
        BattleResultOwners preview = fixture.World.PreviewBattleResultOwners();
        int ability = guardianForce == 0 ? fixture.CompletedAbility.Ability : preview.CharacterVitals.GuardianForceAbilities(1).First(entry => !entry.Learned).Ability;
        Assert.DoesNotContain(preview.Rewards.GuardianForceGrowth, growth => growth.Id == 1 && growth.LearnedAbilityIds.Length > 0);
        Dictionary<int, int> choices = new() { [0] = NextChoice(preview) };
        // Process a valid choice first to catch publication of partially applied selections.
        choices[guardianForce] = ability;
        string before = JsonSerializer.Serialize(fixture.Session.State);

        Assert.Throws<InvalidOperationException>(() => fixture.World.ApplyBattleResult(fixture.Result.Encounter, fixture.Result, choices));

        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
        Assert.Null(fixture.World.LastBattleRewards);
        Assert.Equal(JsonSerializer.Serialize(preview), JsonSerializer.Serialize(fixture.World.PreviewBattleResultOwners()));
    }

    [Fact]
    public void StaleOccurrenceRejectsRewardAndLearningPublication()
    {
        using Fixture fixture = CreateFixture();
        BattleResultOwners preview = fixture.World.PreviewBattleResultOwners();
        BattleEncounterOccurrence stale = fixture.Result.Encounter with { OccurrenceId = "stale-learning-component" };
        string before = JsonSerializer.Serialize(fixture.Session.State);

        Assert.Throws<InvalidOperationException>(() => fixture.World.ApplyBattleResult(stale, fixture.Result with { Encounter = stale }, new Dictionary<int, int> { [0] = NextChoice(preview) }));

        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
        Assert.Null(fixture.World.LastBattleRewards);
    }

    private static int NextChoice(BattleResultOwners preview)
    {
        return preview.CharacterVitals.GuardianForceAbilities(0).First(entry => !entry.Learned && entry.Ability != 0 && entry.Ability != preview.CharacterVitals.GuardianForces[0].Learning).Ability;
    }

    private static Fixture CreateFixture()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).AcquireGuardianForce(1).ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(0, 1).RebuildParty().Rest();
        NativeGuardianForceAbility completing = vitals.GuardianForceAbilities(0).First(entry => !entry.Learned && entry.Ability != 0 && entry.RequiredPoints >= 20);
        NativeGuardianForceAbility incomplete = vitals.GuardianForceAbilities(1).First(entry => !entry.Learned && entry.Ability != 0 && entry.RequiredPoints >= 20);
        vitals = vitals.SelectGuardianForceLearning(0, completing.Ability).SelectGuardianForceLearning(1, incomplete.Ability);
        vitals = vitals.AwardGuardianForceAbilityPoints(0, checked((ushort)(completing.RequiredPoints - 1 - completing.Progress)));
        GameSession session = new(new GameState { InputConfiguration = assets.ReadNewGameInputConfiguration(), NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory(), Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance(), Economy = assets.ReadNewGameEconomy(), BattleVariables = NativeBattleVariableState.CreateNewGame(), LimitProgress = assets.ReadNewGameLimitProgress(), SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(), PocketStation = assets.ReadNewGamePocketStation(), ScannedModels = assets.ReadNewGameScannedModels(), ShotAvailability = assets.ReadNewGameShotAvailability(), GlobalRandom = assets.ReadNewGameGlobalRandom(), KnownMagic = assets.ReadNewGameKnownMagic() });
        BattleResult result;
        using (GameWorld setup = new(session, assets.GameDirectory))
        {
            BattleDefinition original = setup.CreateEncounterBattle(512);
            // Explicit combat inputs accelerate a real victory and retain the native defeat and AP receipts.
            BattleDefinition definition = original with { NativeGraphics = null, NativeClockRules = null, Combatants = original.Combatants.Select(actor => actor.IsPlayer ? actor with { Attack = 255, PhysicalPower = 255, Speed = 10_000, InitialAtb = 10_000 } : actor with { CurrentHp = 1, Speed = 1 }).ToArray() };
            BattleSession active = GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.Normal);
            for (int frame = 0; frame < 1000 && active.Outcome == BattleOutcome.Active; frame++)
            {
                BattleCombatant? player = active.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady);
                BattleCombatant? enemy = active.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.IsAlive);
                if (player is not null && enemy is not null)
                {
                    active.QueueAttack(player.Id, enemy.Id);
                }
                active.Tick();
            }
            Assert.Equal(BattleOutcome.Victory, active.Outcome);
            BattleEncounterOccurrence occurrence = new("learning-commit-component", 512, setup.BattleResources.FormationSourceHash(512), SceneKind.Field, 999, 0, new string('A', 64), null);
            session.SetPendingBattleEncounter(occurrence);
            session.SetBattle(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(active.Capture()))!);
            result = setup.CaptureBattleOutcome(active, occurrence);
        }
        return new Fixture(session, new GameWorld(session, assets.GameDirectory), result, completing);
    }

    private sealed record Fixture(GameSession Session, GameWorld World, BattleResult Result, NativeGuardianForceAbility CompletedAbility) : IDisposable
    {
        public void Dispose()
        {
            World.Dispose();
        }
    }
}
