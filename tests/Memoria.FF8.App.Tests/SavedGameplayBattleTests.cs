using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SavedGameplayBattleTests
{
    [Theory]
    [InlineData(BattleAtbMode.Normal, BattleGunbladeMode.Automatic, false)]
    [InlineData(BattleAtbMode.TurnBased, BattleGunbladeMode.Manual, true)]
    public void Live_battle_initializes_from_its_save_choices_and_snapshot_keeps_them(BattleAtbMode mode, BattleGunbladeMode gunblade, bool guardianForms)
    {
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        };
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, new GameplayPreferences { AtbMode = mode, GunbladeMode = gunblade, GuardianFormsEnabled = guardianForms });
        Assert.Equal(mode, battle.Mode);
        Assert.Equal(gunblade, battle.GunbladeMode);
        Assert.Equal(guardianForms, battle.GuardianFormsEnabled);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal(mode, restored.Mode);
        Assert.Equal(gunblade, restored.GunbladeMode);
        Assert.Equal(guardianForms, restored.GuardianFormsEnabled);
    }
}
