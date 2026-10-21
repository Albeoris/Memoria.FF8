using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class SmartAttackCatalogTests
{
    [Theory]
    [InlineData("Fire", true)]
    [InlineData("Fira", true)]
    [InlineData("Firaga", true)]
    [InlineData("Blizzard", true)]
    [InlineData("Blizzara", true)]
    [InlineData("Blizzaga", true)]
    [InlineData("Thunder", true)]
    [InlineData("Thundara", true)]
    [InlineData("Thundaga", true)]
    [InlineData("Water", true)]
    [InlineData("Aero", true)]
    [InlineData("Holy", true)]
    [InlineData("Flare", true)]
    [InlineData("Quake", false)]
    [InlineData("Tornado", false)]
    [InlineData("Ultima", false)]
    [InlineData("Apocalypse", false)]
    [InlineData("Meteor", false)]
    [InlineData("Bio", false)]
    [InlineData("Meltdown", false)]
    [InlineData("Demi", false)]
    [InlineData("Drain", false)]
    [InlineData("Cure", false)]
    [InlineData("Life", false)]
    public void InstalledCatalogMetadataDeterminesSupportedStockCandidates(string name, bool eligible)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "kernel.bin")) || File.Exists(Path.Combine(directory, "Data", "lang-en", "main.fs")), "Optional installed spell catalog is not configured.");
        using BattleAssets assets = new(directory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == name);
        Assert.InRange(action.Id, 1, 56);
        Assert.Equal(action.Id, action.NativeAction!.ActionId);
        Assert.Equal(eligible, BattleSession.IsSmartSpell(action));
    }
}
