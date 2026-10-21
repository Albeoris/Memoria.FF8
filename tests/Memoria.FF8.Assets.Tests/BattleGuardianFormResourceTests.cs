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
    }
}
