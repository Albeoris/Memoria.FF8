using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDoom8PresentationTests
{
    [Fact]
    public void Original_Doom_component_completes_controlled_contact_and_original_resources_after_cold_continuation()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([8], "MEMORIA_FF8_DOOM8", SelectOriginalFixture);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)8, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 263 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 263);
        BattleEncounter formation = assets.LoadEncounter(509);
        Assert.Equal(142, formation.StageId);
        Assert.NotNull(assets.LoadStage(formation.StageId).NativeSource);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true), enemy => enemy.Slot == 1);
        Assert.Equal(122, caster.ModelId);
        Assert.Equal(new NativeBattleVector(0, 0, -3400), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal(70, assets.ReadEnemyNativeGraphicsResource(caster.ModelId).PoseSource!.BoneCount);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(caster.ModelId, caster.Level);
        Assert.Equal((byte)11, NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action));
        return new("doom8", effect, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 4, PreserveOriginalFormationEnemies = true, CrisisLevel = action.CrisisLevel };
    }
}
