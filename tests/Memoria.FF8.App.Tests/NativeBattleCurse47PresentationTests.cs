using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCurse47PresentationTests
{
    [Fact]
    public void Original_Curse_component_preserves_its_original_caster_formation_and_sequence()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([47], "MEMORIA_FF8_CURSE47", SelectOriginalFixture);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)47, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 86 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 86);
        BattleEncounter formation = assets.LoadEncounter(213);
        Assert.Equal(54, formation.StageId);
        Assert.NotNull(assets.LoadStage(formation.StageId).NativeSource);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(54, enemy.ModelId);
        Assert.Equal(new NativeBattleVector(0, 0, -1800), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        Assert.Equal(23, assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource!.BoneCount);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(enemy.ModelId, enemy.Level);
        Assert.Equal((byte)15, NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action));
        return new("curse47", effect, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, CrisisLevel = action.CrisisLevel };
    }
}
