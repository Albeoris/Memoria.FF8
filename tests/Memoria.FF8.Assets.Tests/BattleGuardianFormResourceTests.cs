using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleGuardianFormResourceTests
{
    [Fact]
    public void InstalledIfritFormUsesTheCavernPrototypeDefensesAndRoundTripsThem()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using BattleAssets assets = new(directory!);
        BattleGuardianFormResources resources = assets.ReadIfritGuardianFormResources();
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(resources.GuardianForceId)!;
        Assert.Equal(61, profile.ModelId);
        Assert.Equal(assets.ReadEnemyNativeStats(profile.ModelId).ElementDefenses.Select(value => (int)value), resources.ElementDefenses);
        Assert.Equal(new[] { 1000, 700, 800, 800, 800, 800, 800, 800 }, resources.ElementDefenses);
        Assert.Equal(assets.ReadEnemyNativeStatusData(profile.ModelId).Resistances, resources.StatusResistances);
        Assert.Equal((byte)255, resources.StatusResistances[1]);
        BattleGuardianFormResources restored = JsonSerializer.Deserialize<BattleGuardianFormResources>(JsonSerializer.Serialize(resources))!;
        Assert.Equal(resources.ElementDefenses, restored.ElementDefenses);
        Assert.Equal(resources.StatusResistances, restored.StatusResistances);
        Assert.Null(restored.AbilityBands);
        Assert.Equal(resources.InnateBindings, restored.InnateBindings);
        Assert.Equal(resources.CommandBindings, restored.CommandBindings);
    }

    [Fact]
    public void InstalledIfritFormBindsOnlyTheSelectedNativeActionsAndLearnableCommands()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using BattleAssets assets = new(directory!);
        BattleGuardianFormResources resources = assets.ReadIfritGuardianFormResources();
        NativeBattleAiDefinition boss = assets.ReadEnemyAi(61, 1);
        Assert.Equal((8, 79, 12), ((int)boss.Abilities[0].Command, (int)boss.Abilities[0].Id, (int)boss.Abilities[0].Sequence));
        Assert.Equal((8, 59, 12, 20), ((int)boss.Abilities[6].Command, (int)boss.Abilities[6].Id, (int)boss.Abilities[6].Sequence, boss.Abilities[6].Power));
        Assert.Equal((2, 1, 11), ((int)boss.Abilities[1].Command, (int)boss.Abilities[1].Id, (int)boss.Abilities[1].Sequence));
        Assert.Equal((79, (byte)12), PhysicalBinding(BattleGuardianFormAction.Attack));
        Assert.Equal((59, (byte)12), PhysicalBinding(BattleGuardianFormAction.HeavyStrike));
        Assert.Equal(new[] { (1, 2), (2, 142), (3, 143), (15, 124) }, resources.InnateBindings.Where(action => action.Source == BattleGuardianFormActionSource.Magic).Select(action =>
        {
            Assert.Equal(BattleGuardianFormActionCost.Innate, action.Cost);
            Assert.Equal((byte)11, action.Sequence);
            BattleNativeAction spell = Assert.Single(assets.NativeActions, native => native.Command == 2 && native.ActionId == action.ResourceActionId);
            return ((int)spell.ActionId, (int)spell.MagicId);
        }));
        Assert.Equal(BattleGuardianFormAction.HellFire, Assert.Single(resources.InnateBindings, action => action.Terminal).Action);
        Assert.Equal(new byte[] { 29, 30, 24, 25, 23, 28, 26, 32, 27, 33, 34, 31, 7, 38 }, resources.CommandBindings.Select(binding => binding.Command));
        Assert.All(resources.CommandBindings, binding => Assert.Equal(assets.ReadNativeCommand(binding.Command), binding));

        (int, byte) PhysicalBinding(BattleGuardianFormAction action)
        {
            BattleGuardianFormActionBinding binding = Assert.Single(resources.InnateBindings, binding => binding.Action == action);
            return (binding.ResourceActionId, binding.Sequence);
        }
    }
}
