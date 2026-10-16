using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAutomaticGuardianTests
{
    [Theory]
    [InlineData(2, 100, 255, 0, 0)]
    [InlineData(2, 200, 0, -1, 0)]
    [InlineData(8, 100, 0, 10, 1)]
    public void EntryBarrierOwnsOdinResistanceAdmissionAndGilgameshQuartileSelection(byte savedFlags, byte ejectResistance, byte deathResistance, int expectedId, byte gilgameshUsed)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        byte[] resistances = Enumerable.Repeat((byte)100, 40).ToArray();
        resistances[0] = deathResistance;
        resistances[24] = ejectResistance;
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party-0", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeStatusResistances = resistances }
        ];
        BattleDefinition definition = Definition(game, assets, savedFlags, actors) with { EncounterId = 94, StageId = formation.StageId, NativeClockRules = assets.ReadNativeClockRules(), NativeBattleSpeed = 0, NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeCrtRandom = new(1), NativeCommandSequences = game.ReadNativeBattleCommandSequences(), NativeStageSource = stage.NativeSource, NativeStageCameraBank = stage.NativeCameraBank, NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera], NativeStageCameraControl = stage.NativeCameraControl, NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation) };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Assert.False(battle.Capture().NativeSupport!.EntryInitialized);
        Assert.Empty(battle.QueuedActions);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(333_334));
        }
        Assert.True(battle.NativeEntryReady);
        NativeBattleSupportState state = battle.Capture().NativeSupport!;
        Assert.True(state.EntryInitialized);
        Assert.Equal((ushort)assets.ReadNativeSupportRules().Cooldown, state.Cooldown);
        Assert.Equal(gilgameshUsed, state.GilgameshUsed);
        if (expectedId < 0)
        {
            Assert.Empty(battle.QueuedActions);
            Assert.Equal(definition.InitialRandom, battle.Capture().Random);
        }
        else
        {
            BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
            Assert.Equal(new BattleNativeCallback(0, 7), queued.NativeCallback);
            Assert.Null(queued.NativeAction);
            Assert.Equal((byte)expectedId, state.SelectedAction);
        }
        Assert.Equal(0, battle.Frame);
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(snapshot, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!).Capture()));
    }

    [Fact]
    public void FatalPartyDamageQueuesPhoenixAndSeparateRevivalAndKeepsTheKoActorAcrossRestore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateFatalBattle(game, assets, 4, 0, 1);
        for (int frame = 0; frame < 128 && !battle.QueuedActions.Any(command => command.NativeCallback?.Stage == 7); frame++)
        {
            battle.Tick();
        }
        Assert.False(battle.Combatants[0].IsAlive);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(new BattleNativeCallback(0, 7), Assert.Single(battle.QueuedActions).NativeCallback);
        Assert.Equal((byte)1, battle.Capture().NativeSupport!.SelectedAction);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 80 && !battle.Combatants[0].IsAlive; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.True(battle.Combatants[0].IsAlive);
        Assert.Equal(new[] { 1, 8 }, battle.Events.Where(item => item.Kind == "Queued" && item.ActorId == "party-0").Select(item => item.Amount));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal((ushort)1, battle.Combatants[0].NativeDeathCount);
    }

    [Theory]
    [InlineData(0, 0, 1, false)]
    [InlineData(4, 317, 1, false)]
    [InlineData(4, 0, 2, true)]
    public void PhoenixNegativeGuardsAndFailedSingleRollPublishDefeatWithoutInventingAnotherAttempt(byte flags, int encounterId, byte randomIndex, bool consumesRoll)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateFatalBattle(game, assets, flags, encounterId, randomIndex);
        NativeBattleRandomState expected = battle.Capture().Random!;
        if (consumesRoll)
        {
            expected = expected.Draw(out _);
        }
        for (int frame = 0; frame < 128 && battle.Outcome == BattleOutcome.Active; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(BattleOutcome.Defeat, battle.Outcome);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(expected.Indexes, battle.Capture().Random!.Indexes);
        battle.Tick();
        Assert.Equal(expected.Indexes, battle.Capture().Random!.Indexes);
    }

    [Theory]
    [InlineData(64, BattleOutcome.Active)]
    [InlineData(65, BattleOutcome.Defeat)]
    public void PhoenixChanceIncludesTheOriginalBoundaryByte(byte roll, BattleOutcome expected)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        byte index = Enumerable.Range(0, 256).Select(value => (byte)value).Single(value =>
        {
            NativeBattleRandomState state = new(Enumerable.Repeat(value, 8).ToImmutableArray(), 0);
            state.Draw(out byte sample);
            return sample == roll;
        });
        BattleSession battle = CreateFatalBattle(game, assets, 4, 0, index);
        for (int frame = 0; frame < 128 && battle.Outcome == BattleOutcome.Active && !battle.QueuedActions.Any(command => command.NativeCallback?.Stage == 7); frame++)
        {
            battle.Tick();
        }
        Assert.Equal(expected, battle.Outcome);
        Assert.Equal(expected == BattleOutcome.Active ? 1 : 0, battle.QueuedActions.Count);
    }

    private static BattleSession CreateFatalBattle(GameAssets game, BattleAssets assets, byte flags, int encounterId, byte randomIndex)
    {
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8) with { ActionId = 1000, AttackType = 27, Power = 1, Hit = 0, HitCount = 1, AttackFlags = 0, Statuses0 = 0, Statuses1 = 0, Element = 0 };
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party-0", Name = "Squall", IsPlayer = true, MaximumHp = 100, CurrentHp = 10, NativeDeathCount = 0, Speed = 1 },
            new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeStatusResistances = new byte[40] }
        ];
        BattleDefinition definition = Definition(game, assets, flags, actors) with { EncounterId = encounterId, NativeActions = [.. assets.NativeActions, attack], InitialRandom = new(Enumerable.Repeat(randomIndex, 8).ToImmutableArray(), 0) };
        BattleSession initial = new(definition, BattleAtbMode.TurnBased);
        BattleAttackCommand hit = new("enemy", "party-0", BattleAbility.EnemyAbility, attack.ActionId) { NativeQueuedCommand = 8, NativeAction = attack, NativeTargetMask = 1 };
        return BattleSession.Restore(initial.Capture() with { NativeSupport = new(true, assets.ReadNativeSupportRules().Cooldown, 0), QueuedActions = [hit] });
    }

    private static BattleDefinition Definition(GameAssets game, BattleAssets assets, byte flags, BattleCombatantDefinition[] actors)
    {
        return new() { NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeCommandSequences = game.ReadNativeBattleCommandSequences(), StageId = 0, NativeFieldBattleRules = 0, NativeActions = assets.NativeActions.ToArray(), ActionDefinitions = assets.ActionDefinitions.ToArray(), NativeSupportRules = assets.ReadNativeSupportRules(), NativeSavedBattleFlags = flags, NativeSavedBattleFlagsKnownMask = 255, InitialRandom = new(Enumerable.Repeat((byte)1, 8).ToImmutableArray(), 0), GuardianForces = [new(0, 100, 100) { KillCount = 0, DeathCount = 0 }], Combatants = actors };
    }
}
