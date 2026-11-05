namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldAllocationCompatibilityTests
{
    [Theory]
    [InlineData("41E4391576C7497EBF13EF0D30C3E21925C2C4E28CB3CD61F6CC82A4A27BCE09", "114171520663E17D2258DD05AC49650A89E254D5D111E64AEBE18CF1847EDF66")]
    [InlineData("114171520663E17D2258DD05AC49650A89E254D5D111E64AEBE18CF1847EDF66", "41E4391576C7497EBF13EF0D30C3E21925C2C4E28CB3CD61F6CC82A4A27BCE09")]
    public void Authenticated_normal_foot_settings_revisions_can_reload_each_other(string source, string target)
    {
        Assert.True(NativeWorldAllocationCompatibility.CanReloadFootBattle(source, target));
        Assert.False(NativeWorldAllocationCompatibility.CanReloadFootBattle(new('A', 64), target));
        Assert.False(NativeWorldAllocationCompatibility.CanReloadFootBattle(source, new('B', 64)));
    }

    [Fact]
    public void Identical_custom_settings_keep_the_existing_identity_contract()
    {
        string identity = new('C', 64);
        Assert.True(NativeWorldAllocationCompatibility.CanReloadFootBattle(identity, identity));
    }
}
