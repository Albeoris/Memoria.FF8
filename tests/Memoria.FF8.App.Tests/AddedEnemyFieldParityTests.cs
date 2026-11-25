using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AddedEnemyFieldParityTests
{
    [Theory]
    [InlineData(701)]
    [InlineData(703)]
    public void AddedEnemyRetainsTheDonorFieldsAndReprojectsItsStatsLikeTheAuthoredEnemy(ushort encounter)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AdditionalEnemies = 1 });

        BattleSession mixed = world.PrepareBattleEntry(encounter, 0, 41, BattleAtbMode.TurnBased).Battle;
        var placement = Assert.Single(mixed.ReadDefinition().AddedEnemySources!);
        BattleEnemyRosterSource source = placement.Value;
        BattleCombatant added = Assert.Single(mixed.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == placement.Key);
        Assert.True(added.IsActive);
        Assert.Null(added.NativeActorIndex);

        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AdditionalEnemies = 0 });
        BattleSession authored = world.PrepareBattleEntry(source.Formation, 0, 41, BattleAtbMode.TurnBased).Battle;
        BattleCombatant original = Assert.Single(authored.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == source.SceneSlot);
        Assert.True(original.IsActive);
        Assert.NotNull(original.NativeActorIndex);
        Assert.Equal(original.ModelId, added.ModelId);
        Assert.Equal(original.MaximumHp, original.CurrentHp);
        Assert.Equal(added.MaximumHp, added.CurrentHp);

        NativeBattleGraphicsModel originalModel = ReadReadyModel(authored, original);
        NativeBattleGraphicsModel addedModel = ReadReadyModel(mixed, added);
        Assert.Equal(originalModel.ModelId4, addedModel.ModelId4);
        Assert.NotNull(originalModel.GeometryBounds52);
        Assert.NotNull(addedModel.GeometryBounds52);
        Assert.Equal(originalModel.ResourceFlags44, addedModel.ResourceFlags44);
        Assert.Equal(originalModel.Rotation14, addedModel.Rotation14);
        Assert.NotNull(addedModel.Transform);
        Assert.NotNull(addedModel.BodyTextureSlots);

        original.BindNativeLevel(added.Level);
        BattleCombatantDefinition addedEntry = mixed.GetCombatantDefinition(added.Id);
        BattleCombatantDefinition expected = authored.GetCombatantDefinition(original.Id) with
        {
            Id = added.Id,
            SceneSlot = placement.Key,
            RewardSlot = placement.Key,
            InitialAtb = addedEntry.InitialAtb,
            CurrentHp = addedEntry.MaximumHp,
            ExpandedEnemyBaseStatistics = addedEntry.ExpandedEnemyBaseStatistics,
            NativeDrawSlots = world.BattleResources.ReadEnemyDrawSlots(original.ModelId, added.Level)
        };
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(addedEntry));

        const int newLevel = 60;
        Assert.NotEqual(newLevel, added.Level);
        original.BindNativeLevel(newLevel);
        added.BindNativeLevel(newLevel);
        Assert.Equal(original.MaximumHp, added.MaximumHp);
        Assert.Equal(ReadStats(original), ReadStats(added));

        original.SetNativeStatMultiplier(0, 15);
        added.SetNativeStatMultiplier(0, 15);
        Assert.Equal(ReadStats(original), ReadStats(added));
        BattleSession restored = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(mixed.Capture()));
        BattleCombatant continued = Assert.Single(restored.Combatants, actor => actor.Id == added.Id);
        Assert.Null(continued.NativeActorIndex);
        Assert.Equal(newLevel, continued.Level);
        Assert.Equal(added.CurrentHp, continued.CurrentHp);
        Assert.Equal(ReadStats(original), ReadStats(continued));
    }

    private static NativeBattleGraphicsModel ReadReadyModel(BattleSession battle, BattleCombatant actor)
    {
        for (int frame = 0; frame < 600 && !battle.NativeEntryReady; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        return battle.Presentation!.Graphics.ActorModels.Get(new BattleEnemyActorModel(new(actor.Id)));
    }

    private static int[] ReadStats(BattleCombatant actor)
    {
        return [actor.Attack, actor.Defense, actor.Magic, actor.Spirit, actor.Speed, actor.Evasion];
    }
}
