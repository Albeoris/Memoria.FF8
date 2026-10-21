using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDevour62PresentationTests
{
    [Fact]
    public void Original_Devour_component_uses_its_real_target_outcome_and_private_quads()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([62], "MEMORIA_FF8_DEVOUR62", SelectOriginalFixture);
    }

    [Fact]
    public void Explicit_contact_outcome_is_limited_to_controlled_post_formula_fixtures()
    {
        NativeEffectFixtureRequest request = new("controlled-outcome", 62, 7, 9, 3, 800, 240);
        Assert.Null(request.ControlledContactValue);
        NativeBattleEffectFixtureExporter.Validate(request);
        NativeBattleEffectFixtureExporter.Validate(request with { ControlledContactValue = 0 });
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { ControlledContactValue = 0, OrdinaryMagicAdmission = true }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { ControlledContactValue = 0, HealingContact = true }));
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)62, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 7 && row.ActionId == 9 && row.MagicId == effect && row.KernelSection == 10 && row.KernelIndex == 9);
        Assert.Equal((byte)19, action.AttackType);
        BattleEncounter formation = assets.LoadEncounter(368);
        Assert.Equal(140, formation.StageId);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(3, enemy.ModelId);
        Assert.Equal(new NativeBattleVector(300, 0, -2100), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        BattleNativeEnemySpecial target = assets.ReadNativeEnemySpecial(enemy.ModelId);
        Assert.Equal(new byte[] { 1, 1, 1 }, target.DevourEffects.ToArray());
        Assert.Equal((byte)0, target.DevourOutcome);
        return new("devour62", effect, action.Command, action.ActionId, 3, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, ControlledContactValue = target.DevourOutcome, CrisisLevel = action.CrisisLevel };
    }
}
