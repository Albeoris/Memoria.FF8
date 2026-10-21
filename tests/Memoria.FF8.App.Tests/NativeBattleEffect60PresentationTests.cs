using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect60PresentationTests
{
    [Fact]
    public void Original_effect60_component_preserves_its_formation_and_restores_its_actual_target()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([60], "MEMORIA_FF8_EFFECT60", SelectOriginalFixture);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)60, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 294 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 294);
        BattleEncounter formation = assets.LoadEncounter(810);
        Assert.Equal(62, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new[] { 137, 136 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.Equal(new NativeBattleVector(-1200, 0, -2500), enemies[0].NativePosition);
        Assert.Equal(new NativeBattleVector(1200, 0, -3400), enemies[1].NativePosition);
        Assert.All(enemies, enemy =>
        {
            Assert.False(enemy.Hidden);
            Assert.False(enemy.InitiallySuspended);
            Assert.False(enemy.Untargetable);
            Assert.NotNull(assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource);
        });
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(enemies[0].ModelId, enemies[0].Level);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action));
        return new("effect60", effect, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, CrisisLevel = action.CrisisLevel };
    }
}
