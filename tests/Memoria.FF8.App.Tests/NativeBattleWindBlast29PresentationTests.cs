using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleWindBlast29PresentationTests
{
    [Fact]
    public void Original_Wind_Blast_component_preserves_its_original_formation_and_native_producer()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([29], "MEMORIA_FF8_WIND_BLAST29", SelectOriginalFixture);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)29, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 133 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 133);
        BattleEncounter formation = assets.LoadEncounter(368);
        Assert.Equal(140, formation.StageId);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(3, enemy.ModelId);
        Assert.Equal(new NativeBattleVector(300, 0, -2100), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        Assert.NotNull(assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(enemy.ModelId, enemy.Level);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action));
        return new("wind-blast29", effect, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, CrisisLevel = action.CrisisLevel };
    }
}
