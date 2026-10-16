using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    public void ConfigureGameplayPreferences(GameplayPreferences preferences)
    {
        session.SetGameplayPreferences(preferences);
        if (battle is not null)
        {
            battle.SetMode(preferences.AtbMode);
            battle.SetGunbladeMode(preferences.GunbladeMode);
            session.SetBattle(battle.Capture());
        }
    }

    internal static BattleSession CreateLiveBattleSession(BattleDefinition definition, GameplayPreferences preferences)
    {
        preferences.Validate();
        BattleSession active = CreateLiveBattleSession(definition, preferences.AtbMode);
        active.SetGunbladeMode(preferences.GunbladeMode);
        return active;
    }
}
