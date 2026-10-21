using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMeltingBubble43PresentationTests
{
    [Fact]
    public void Original_Melting_Bubble_component_preserves_its_original_formation_and_absolute_resources()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([43], "MEMORIA_FF8_MELTING_BUBBLE43", SelectOriginalFixture);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)43, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 157 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 157);
        BattleEncounter formation = assets.LoadEncounter(146);
        Assert.Equal(21, formation.StageId);
        Assert.NotNull(assets.LoadStage(formation.StageId).NativeSource);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(37, enemy.ModelId);
        Assert.Equal(new NativeBattleVector(0, 0, -2500), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        Assert.NotNull(assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(enemy.ModelId, enemy.Level);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action));
        return new("melting-bubble43", effect, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, CrisisLevel = action.CrisisLevel };
    }
}
