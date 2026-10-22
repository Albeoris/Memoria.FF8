using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleWhiteWind297PresentationTests
{
    [Fact]
    public void Original_Blue_Magic_White_Wind_presents_declared_healing_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([297], "MEMORIA_FF8_WHITE_WIND297", SelectOriginalFixture);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)297, effect);
        BattleNativeAction[] crisis = assets.NativeActions.Where(row => row.Command == 15 && row.ActionId == 11 && row.MagicId == effect && row.KernelSection == 19 && row.KernelIndex == 11).OrderBy(row => row.CrisisLevel).ToArray();
        Assert.Equal(4, crisis.Length);
        for (int index = 0; index < crisis.Length; index++)
        {
            Assert.Equal((byte?)(index + 1), crisis[index].CrisisLevel);
            Assert.Equal((byte)25, crisis[index].AttackType);
            Assert.Equal((byte)10, crisis[index].Power);
            Assert.Equal((byte?)255, crisis[index].Hit);
            Assert.Equal((ushort)0, crisis[index].Statuses0);
            Assert.Equal(0u, crisis[index].Statuses1);
            Assert.Equal(44 + index, crisis[index].SupplementalKernelIndex);
            Assert.Equal(3, crisis[index].CharacterId);
        }
        BattleNativeAction action = crisis[0];
        BattleEncounter formation = assets.LoadEncounter(368);
        Assert.Equal(140, formation.StageId);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(3, enemy.ModelId);
        Assert.Equal(new NativeBattleVector(300, 0, -2100), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        return new("white-wind297-healing", effect, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, PartyModelId = action.CharacterId!.Value, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = action.CrisisLevel, HealingContact = true };
    }
}
