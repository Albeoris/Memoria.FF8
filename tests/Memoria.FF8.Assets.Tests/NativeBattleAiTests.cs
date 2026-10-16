using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAiTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void OriginalSpecialActorVariableAliasFailsAtItsPreciseUnsupportedMemoryBoundary()
    {
        using BattleAssets assets = new(GameDirectory);
        NativeBattleAiDefinition program = assets.ReadEnemyAi(71, 7);
        BattleDefinition definition = new() { EncounterId = 105, Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Original model71", ModelId = 71, NativeAi = program }] };
        Assert.Contains("eight-DWORD bank", Assert.Throws<NotSupportedException>(() => new BattleSession(definition, BattleAtbMode.Normal)).Message);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(514)]
    [InlineData(515)]
    [InlineData(516)]
    [InlineData(517)]
    [InlineData(518)]
    [InlineData(519)]
    [InlineData(520)]
    [InlineData(94)]
    [InlineData(34)]
    [InlineData(37)]
    [InlineData(521)]
    [InlineData(36)]
    [InlineData(38)]
    [InlineData(39)]
    [InlineData(40)]
    [InlineData(48)]
    [InlineData(57)]
    public void OriginalGardenIslandAndReachedEnemiesExecuteTheirInstalledInitialAndTurnPrograms(int encounterId)
    {
        using BattleAssets assets = new(GameDirectory);
        List<BattleCombatantDefinition> actors = [new() { Id = "party-0", Name = "Squall", ModelId = 0, IsPlayer = true, Level = 7, MaximumHp = 1_000_000, CurrentHp = 1_000_000 }];
        foreach (BattleEnemyPlacement placement in assets.LoadEncounter(encounterId).Enemies)
        {
            int level = NativeBattleEnemyLevelRules.Resolve((byte)placement.Level, [7], 0, 0);
            BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(placement.ModelId).Project(level);
            actors.Add(new() { Id = $"enemy-{placement.Slot}", Name = assets.ReadEnemyName(placement.ModelId), ModelId = placement.ModelId, SceneSlot = placement.Slot, InitialHidden = placement.Hidden, InitialSuspended = placement.InitiallySuspended, InitialUntargetable = placement.Untargetable, Level = level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = 10_000, InitialAtb = 10_000, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeAi = assets.ReadEnemyAi(placement.ModelId, level), NativeTemplate = assets.ReadEnemyTemplate(placement.ModelId, (byte)placement.Level) });
        }
        // Explicit synthetic copy of the proved native New Game clear, not a missing-save default.
        BattleSession battle = new(new() { Name = $"Installed formation {encounterId}", EncounterId = encounterId, PersistentAiVariables = new uint[8], Combatants = actors.ToArray(), ActionDefinitions = assets.ActionDefinitions.ToArray() }, BattleAtbMode.Normal);
        for (int frame = 0; frame < 200; frame++)
        {
            battle.Tick();
        }
        Assert.Contains(battle.Capture().Combatants, actor => actor.Id.StartsWith("enemy-") && actor.NativeParticipant!.TurnCount > 0);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(battle.Capture()), System.Text.Json.JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Fact]
    public void OriginalTowerAiActivatesRetainedWedgeRevivesBiggsAndAllocatesElvoretBeforeVictory()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleSession battle = new(CreateTower(assets), BattleAtbMode.Normal);
        Assert.True(battle.Combatants.Single(actor => actor.SceneSlot == 1).Suspended);
        Assert.False(battle.Combatants.Single(actor => actor.SceneSlot == 2).IsActive);
        Assert.Throws<InvalidOperationException>(() => battle.QueueAttack("party-0", "enemy-1"));
        Strike(battle, "enemy-0");
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.True(battle.Combatants.Single(actor => actor.SceneSlot == 0).IsAlive);
        Assert.False(battle.Combatants.Single(actor => actor.SceneSlot == 1).Suspended);
        Assert.False(battle.Combatants.Single(actor => actor.SceneSlot == 1).Hidden);
        Assert.Single(battle.EnemyDefeats, defeat => defeat.Slot == 0);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(battle.Capture()), System.Text.Json.JsonSerializer.Serialize(restored.Capture()));
        Strike(battle, "enemy-0");
        Strike(restored, "enemy-0");
        Assert.False(battle.Combatants.Single(actor => actor.SceneSlot == 0).IsActive);
        Assert.False(battle.Combatants.Single(actor => actor.SceneSlot == 1).IsActive);
        BattleCombatant elvoret = battle.Combatants.Single(actor => actor.SceneSlot == 2);
        Assert.True(elvoret.IsAlive && !elvoret.Hidden && !elvoret.Untargetable);
        Assert.InRange(elvoret.Level, 1, 11);
        Assert.Single(battle.EnemyDefeats, defeat => defeat.Slot == 0);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(battle.Capture()), System.Text.Json.JsonSerializer.Serialize(restored.Capture()));
        Strike(battle, "enemy-2");
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Contains(battle.ScriptedRewards, reward => reward.Id == 178 && reward.Count == 1);
        Assert.Equal(new[] { 0, 2 }, battle.EnemyDefeats.Select(defeat => defeat.Slot));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(battle.Capture()), System.Text.Json.JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Fact]
    public void SharedActivationProgramUsesFormationSlotsRatherThanEnemyIdentityOrEncounterNumber()
    {
        NativeBattleAiDefinition program = new([[27, 6, 0, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleDefinition definition = new()
        {
            Name = "Different native formation topology",
            EncounterId = 876,
            Combatants =
            [
                new() { Id = "party-0", Name = "Party", IsPlayer = true },
                new() { Id = "other", Name = "Other monster", SceneSlot = 2, ModelId = 32, NativeAi = program },
                new() { Id = "reserve", Name = "Suspended monster", SceneSlot = 6, ModelId = 5, InitialHidden = true, InitialSuspended = true, InitialUntargetable = true }
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        BattleCombatant activated = battle.Combatants.Single(actor => actor.SceneSlot == 6);
        Assert.True(activated.IsAlive && !activated.Hidden && !activated.Suspended && !activated.Untargetable);
        Assert.Contains(battle.Events, item => item.Kind == "Activated" && item.ActorId == "reserve");
    }

    [Fact]
    public void UnsupportedReachedAiOpcodeRejectsInsteadOfUsingPrimitiveEnemyFallback()
    {
        NativeBattleAiDefinition program = new([[255, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleDefinition definition = new() { Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", NativeAi = program }] };
        Assert.Contains("opcode 255", Assert.Throws<NotSupportedException>(() => new BattleSession(definition, BattleAtbMode.Normal)).Message);
    }

    private static BattleDefinition CreateTower(BattleAssets assets)
    {
        BattleEncounter formation = assets.LoadEncounter(29);
        List<BattleCombatantDefinition> actors = [new() { Id = "party-0", Name = "Squall", ModelId = 0, IsPlayer = true, Level = 7, MaximumHp = 100_000, CurrentHp = 100_000, Speed = 10_000, Attack = 255, PhysicalPower = 255, InitialAtb = 10_000 }];
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(0);
        foreach (BattleEnemyPlacement placement in formation.Slots.Take(3))
        {
            byte roll = 0;
            if (placement.Enabled)
            {
                random = random.Draw(out roll);
            }
            int level = placement.Enabled ? NativeBattleEnemyLevelRules.Resolve((byte)placement.Level, [7], roll) : 0;
            BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(placement.ModelId).Project(level);
            actors.Add(new() { Id = $"enemy-{placement.Slot}", Name = assets.ReadEnemyName(placement.ModelId), ModelId = placement.ModelId, SceneSlot = placement.Slot, InitialActive = placement.Enabled, InitialHidden = placement.Hidden, InitialSuspended = placement.InitiallySuspended, InitialUntargetable = placement.Untargetable, Level = level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = Math.Max(1, stats.Speed), ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeAi = assets.ReadEnemyAi(placement.ModelId, level), NativeTemplate = assets.ReadEnemyTemplate(placement.ModelId, (byte)placement.Level), RewardSlot = placement.Slot, Rewards = assets.ReadEnemyRewards(placement.ModelId) });
        }
        return new() { Name = "Original tower formation", EncounterId = 29, StageId = formation.StageId, Combatants = actors.ToArray(), InitialRandom = random, ActionDefinitions = assets.ActionDefinitions.ToArray() };
    }

    private static void Strike(BattleSession battle, string target)
    {
        int guard = 0;
        while (!battle.Combatants.Single(actor => actor.Id == "party-0").IsReady || battle.ActiveAction is not null || battle.QueuedActions.Count != 0)
        {
            battle.Tick();
            Assert.True(++guard < 1000);
        }
        battle.QueueAttack("party-0", target);
        for (int frame = 0; frame < BattleSession.AttackWindupFrames + 2; frame++)
        {
            battle.Tick();
        }
    }
}
