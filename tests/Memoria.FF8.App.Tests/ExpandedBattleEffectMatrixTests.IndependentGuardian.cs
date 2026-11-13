using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(15)]
    public void Installed_independent_guardian_item_uses_its_saved_effect_variant(ushort childId)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction child = Assert.Single(assets.NativeActions, action => action is { KernelSection: 9, Command: 244 } && action.ActionId == childId);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), effect => effect.Effect == child.MagicId);
        (string status, string detail) = RunIndependentGuardianItem(directory, PrepareField(directory), assets, effect, child);
        Assert.True(status is "native" or "logical", detail);
    }

    private static (string Status, string Detail) RunIndependentGuardianItem(string directory, GameState field, BattleAssets assets, NativeBattleEffectBinding effect, BattleNativeAction child)
    {
        IndependentGuardianItemRoute? route = ScenarioDiscovery.ResolveIndependentGuardianItemRoute(child, assets.NativeActions, out string? blocker);
        Assert.True(route is not null, blocker);
        BattleActionDefinition item = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Item && action.NativeAction == route.Item);
        if (route.PocketStationAttackVariant is byte variant)
        {
            NativePocketStationState pocket = Assert.IsType<NativePocketStationState>(field.PocketStation);
            field = field with { PocketStation = pocket with { Raw = pocket.Raw.SetItem(0, (byte)((pocket.Flags | 1) & ~2)).SetItem(1, 10).SetItem(45, variant) } };
        }
        else
        {
            field = field with { CharacterVitals = field.CharacterVitals!.AcquireGuardianForce(0) };
        }
        return RunPlayerCast(directory, field, effect, item, child);
    }
}
