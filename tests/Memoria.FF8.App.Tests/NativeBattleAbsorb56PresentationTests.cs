using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAbsorb56PresentationTests
{
    [Fact]
    public void Original_Absorb_component_presents_both_declared_drain_halves_and_cold_continuations()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([56], "MEMORIA_FF8_ABSORB56", SelectOriginalFixture);
    }

    [Fact]
    public void Controlled_drain_pair_cannot_override_ordinary_or_unrelated_contacts()
    {
        NativeEffectFixtureRequest request = new("absorb-pair", 56, 32, 5, 3, 800, 240) { ControlledDrainContact = true };
        NativeBattleEffectFixtureExporter.Validate(request);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { OrdinaryMagicAdmission = true }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { HealingContact = true }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { ControlledContactValue = 0 }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { Effect = 39 }));
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)56, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 32 && row.ActionId == 5 && row.MagicId == effect && row.KernelSection == 10 && row.KernelIndex == 5);
        Assert.Equal((byte)2, action.AttackType);
        Assert.Equal((byte)200, action.StatusAttack);
        Assert.Equal(0x8000u, action.Statuses1 & 0x8000u);
        BattleEncounter formation = assets.LoadEncounter(368);
        Assert.Equal(140, formation.StageId);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(3, enemy.ModelId);
        Assert.Equal(new NativeBattleVector(300, 0, -2100), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        return new("absorb56", effect, action.Command, action.ActionId, 3, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, ControlledDrainContact = true, CrisisLevel = action.CrisisLevel };
    }
}
