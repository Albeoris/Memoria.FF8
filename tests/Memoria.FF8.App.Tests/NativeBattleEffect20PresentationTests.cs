using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect20PresentationTests
{
    [Fact]
    public void Original_effect20_component_preserves_its_original_suspended_actor_and_native_producer()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([20], "MEMORIA_FF8_EFFECT20", SelectOriginalFixture);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)20, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 287 && row.MagicId == 20 && row.KernelSection == 3 && row.KernelIndex == 287);
        BattleEncounter formation = assets.LoadEncounter(63);
        Assert.Equal(13, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new[] { 68, 81, 80, 77 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.All(enemies.Take(3), enemy =>
        {
            Assert.False(enemy.Hidden);
            Assert.False(enemy.InitiallySuspended);
            Assert.False(enemy.Untargetable);
        });
        Assert.True(enemies[3].Hidden);
        Assert.True(enemies[3].InitiallySuspended);
        Assert.True(enemies[3].Untargetable);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(enemies[0].ModelId, enemies[0].Level);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action));
        foreach (BattleEnemyPlacement enemy in enemies)
        {
            Assert.NotNull(assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource);
        }
        return new("effect20", effect, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PartyModelId = 0, CrisisLevel = action.CrisisLevel, PreserveOriginalFormationEnemies = true };
    }
}
