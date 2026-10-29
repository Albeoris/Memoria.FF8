using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class OmegaEncounterTests
{
    [Theory]
    [InlineData(7)]
    [InlineData(100)]
    public void Installed_Omega_initialization_and_turn_enqueue_survive_independent_Json_continuation(int partyLevel)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        using GameAssets game = new(TestResources.GameDirectory);
        BattleSession battle = new(CreateComponent(assets, game, partyLevel), BattleAtbMode.Normal);
        battle.Tick();
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        BattleCombatant omega = Assert.Single(battle.Combatants, actor => !actor.IsPlayer);
        Assert.Equal(114, omega.ModelId);
        Assert.Empty(battle.QueuedActions);
        BattleSnapshot initialized = battle.Capture();
        Assert.False(initialized.Combatants.Single(actor => actor.Id == omega.Id).NativeParticipant!.DeathHandled);
        BattleSnapshot turn = QueueCallback(initialized, omega.Id, 1);
        BattleSession original = BattleSession.Restore(turn);
        BattleSession restored = RestoreJson(turn);
        original.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(restored.Capture()));
        BattleAttackCommand action = Assert.Single(original.QueuedActions, command => command.NativeCallback is null);
        Assert.Equal(omega.Id, action.ActorId);
        Assert.NotNull(action.NativeAction);
        Assert.Equal(BattleOutcome.Active, original.Outcome);
        Assert.Null(original.ActiveAction);
        Assert.Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(RestoreJson(original.Capture()).Capture()));
        HashSet<int> enqueuedIds = [action.ActionId];
        for (int turnIndex = 1; turnIndex < 64; turnIndex++)
        {
            // Diagnostic callback sequence: explicitly discard the prior output packet without presenting or resolving its effect.
            // Restore the declared party input for the next independent enqueue, retaining the actual enemy interpreter variables and random state.
            BattleSnapshot current = original.Capture();
            BattleSnapshot next = QueueCallback(current with
            {
                QueuedActions = [],
                NativePresentationStatusMemory = current.NativePresentationStatusMemory!.Observe(initialized.Combatants.Single(actor => actor.Id == "party").NativeParticipant!.ActorIndex, initialized.Combatants.Single(actor => actor.Id == "party").NativeStatus!.Presentation!),
                Combatants = current.Combatants.Select(actor => actor.Id == "party" ? actor with { CurrentHp = 9999, NativeStatus = initialized.Combatants.Single(source => source.Id == "party").NativeStatus, StatusEffects = [] } : actor).ToArray()
            }, omega.Id, 1);
            original = BattleSession.Restore(next);
            restored = RestoreJson(next);
            original.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(restored.Capture()));
            BattleAttackCommand[] output = original.QueuedActions.Where(command => command.NativeCallback is null).ToArray();
            Assert.InRange(output.Length, 0, 1);
            if (output.Length == 0)
            {
                continue;
            }
            BattleAttackCommand queued = output[0];
            Assert.Null(original.ActiveAction);
            Assert.NotNull(queued.NativeAction);
            enqueuedIds.Add(queued.ActionId);
        }
        Assert.Equal(new[] { 2, 16, 124, 308, 309, 310, 311, 378 }, enqueuedIds.Order().ToArray());
        // Native enqueue includes logical formula production; no graphical effect dispatch or playable victory is asserted.
    }

    [Fact]
    public void Installed_Omega_death_callback_publishes_tutorial_receipt_once_across_Json_continuation()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        using GameAssets game = new(TestResources.GameDirectory);
        BattleSession entry = new(CreateComponent(assets, game, 100), BattleAtbMode.Normal);
        entry.Tick();
        BattleSnapshot saved = entry.Capture();
        string omega = Assert.Single(entry.Combatants, actor => !actor.IsPlayer).Id;
        // Declared diagnostic post-contact component. No damage producer, route, completed fight or victory is asserted.
        saved = saved with { Combatants = saved.Combatants.Select(actor => actor.Id == omega ? actor with { CurrentHp = 0, Atb = 0 } : actor).ToArray() };
        saved = QueueCallback(saved, omega, 3);
        BattleSession original = BattleSession.Restore(saved);
        BattleSession restored = RestoreJson(saved);
        original.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(new[] { 127 }, original.NativeScriptState.UnlockedTutorials);
        Assert.True(original.Capture().Combatants.Single(actor => actor.Id == omega).NativeParticipant!.DeathHandled);
        Assert.Equal(new[] { 127 }, RestoreJson(original.Capture()).NativeScriptState.UnlockedTutorials);
    }

    private static BattleDefinition CreateComponent(BattleAssets assets, GameAssets game, int partyLevel)
    {
        BattleEncounter encounter = assets.LoadEncounter(462);
        BattleEnemyPlacement placement = Assert.Single(encounter.Enemies);
        Assert.Equal(114, placement.ModelId);
        int level = NativeBattleEnemyLevelRules.Resolve((byte)placement.Level, [(byte)partyLevel], 0, 0);
        BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(placement.ModelId).Project(level);
        return new()
        {
            Name = "Installed Omega initialization and AI component",
            EncounterId = 462,
            StageId = encounter.StageId,
            PersistentAiVariables = new uint[8],
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeMessageSpeed = 1,
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            NativeActions = assets.NativeActions.ToArray(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", ModelId = 0, IsPlayer = true, NativeDeathCount = game.ReadNewGameCharacterVitals().Records[0].DeathCount, Level = partyLevel, MaximumHp = 9999, CurrentHp = 9999, NativeStatusResistances = Enumerable.Repeat((byte)100, 40).ToArray() },
                new() { Id = "omega", Name = assets.ReadEnemyName(114), ModelId = 114, SceneSlot = placement.Slot, Level = level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = stats.Speed, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeAi = assets.ReadEnemyAi(114, level), NativeTemplate = assets.ReadEnemyTemplate(114, (byte)placement.Level) }
            ]
        };
    }

    private static BattleSnapshot QueueCallback(BattleSnapshot saved, string actorId, ushort stage)
    {
        byte actorIndex = checked((byte)saved.Combatants.Single(actor => actor.Id == actorId).NativeParticipant!.ActorIndex);
        NativeBattleCommandBanks banks = NativeBattleCommandBanks.Initialize();
        (banks, NativeBattleCommandLocation location) = banks.EnqueueCallback(0, actorIndex, stage);
        BattleAttackCommand callback = new(actorId, actorId, BattleAbility.EnemyAbility) { NativeCallback = new(actorIndex, stage) };
        return saved with { NativeCommandBanks = banks.BindCommand(location, 0, callback) };
    }

    private static BattleSession RestoreJson(BattleSnapshot saved)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
    }
}
