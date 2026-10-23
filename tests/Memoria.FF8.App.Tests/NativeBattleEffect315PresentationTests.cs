using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect315PresentationTests
{
    [Fact]
    public void Original_Effect315_self_target_component_preserves_loaded_pair_and_native_recipient_activation()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([315], "MEMORIA_FF8_EFFECT315", SelectOriginalFixture);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)315, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 151 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 151);
        BattleEncounter formation = assets.LoadEncounter(104);
        Assert.Equal(19, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new[] { 75, 34 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.All(enemies, enemy => Assert.Equal(new NativeBattleVector(600, 0, -1600), enemy.NativePosition));
        Assert.False(enemies[0].Hidden);
        Assert.False(enemies[0].InitiallySuspended);
        Assert.False(enemies[0].Untargetable);
        Assert.True(enemies[1].Hidden);
        Assert.True(enemies[1].InitiallySuspended);
        Assert.True(enemies[1].Untargetable);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(75, enemies[0].Level), action));
        return new("effect315", effect, 8, 151, 3, 1400, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }
}
