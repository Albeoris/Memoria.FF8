using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SavedGameplayBattleTests
{
    [Theory]
    [InlineData(BattleAtbMode.Normal, BattleGunbladeMode.Automatic, false, BattleDifficulty.Normal, false, false)]
    [InlineData(BattleAtbMode.TurnBased, BattleGunbladeMode.Manual, true, BattleDifficulty.Hard, true, true)]
    public void Live_battle_initializes_from_its_save_choices_and_snapshot_keeps_them(BattleAtbMode mode, BattleGunbladeMode gunblade, bool guardianForms, BattleDifficulty difficulty, bool limitOncePerTurn, bool drawOncePerSpell)
    {
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        };
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, new GameplayPreferences { LimitOncePerTurn = limitOncePerTurn, DrawOncePerSpell = drawOncePerSpell, Difficulty = difficulty, AtbMode = mode, GunbladeMode = gunblade, GuardianFormsEnabled = guardianForms });
        Assert.Equal(limitOncePerTurn, battle.Capture().Definition.LimitOncePerTurn);
        Assert.Equal(drawOncePerSpell, battle.Capture().Definition.DrawOncePerSpell);
        Assert.Equal(difficulty, battle.Difficulty);
        Assert.Equal(mode, battle.Mode);
        Assert.Equal(gunblade, battle.GunbladeMode);
        Assert.Equal(guardianForms, battle.GuardianFormsEnabled);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal(limitOncePerTurn, restored.Capture().Definition.LimitOncePerTurn);
        Assert.Equal(drawOncePerSpell, restored.Capture().Definition.DrawOncePerSpell);
        Assert.Equal(difficulty, restored.Difficulty);
        Assert.Equal(mode, restored.Mode);
        Assert.Equal(gunblade, restored.GunbladeMode);
        Assert.Equal(guardianForms, restored.GuardianFormsEnabled);
    }
}
