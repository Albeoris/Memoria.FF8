using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMicroMissiles299PresentationTests
{
    [Fact]
    public void Original_Micro_Missiles_component_presents_declared_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([299], "MEMORIA_FF8_MICRO_MISSILES299", SelectOriginalFixture);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)299, effect);
        BattleNativeAction[] crisis = assets.NativeActions.Where(row => row.Command == 15 && row.ActionId == 6 && row.MagicId == effect && row.KernelSection == 19 && row.KernelIndex == 6).OrderBy(row => row.CrisisLevel).ToArray();
        Assert.Equal(4, crisis.Length);
        byte[] powers = [8, 12, 14, 15];
        for (int index = 0; index < crisis.Length; index++)
        {
            Assert.Equal((byte?)(index + 1), crisis[index].CrisisLevel);
            Assert.Equal((byte)8, crisis[index].AttackType);
            Assert.Equal(powers[index], crisis[index].Power);
            Assert.Equal((byte?)255, crisis[index].Hit);
            Assert.Equal((ushort)0, crisis[index].Statuses0);
            Assert.Equal(0u, crisis[index].Statuses1);
            Assert.Equal(24 + index, crisis[index].SupplementalKernelIndex);
            Assert.Equal(3, crisis[index].CharacterId);
        }
        BattleNativeAction action = crisis[0];
        BattleEncounter formation = assets.LoadEncounter(368);
        Assert.Equal(140, formation.StageId);
        BattleEnemyPlacement target = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(3, target.ModelId);
        Assert.Equal(new NativeBattleVector(300, 0, -2100), target.NativePosition);
        Assert.False(target.Hidden);
        Assert.False(target.InitiallySuspended);
        Assert.False(target.Untargetable);
        Assert.InRange(assets.ReadEnemyNativeGraphicsResource(target.ModelId).PoseSource!.BoneCount, 1, 256);
        return new("micro-missiles299", effect, action.Command, action.ActionId, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = action.CharacterId!.Value, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = action.CrisisLevel };
    }
}
