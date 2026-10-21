using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    public void ConfigureGameplayPreferences(GameplayPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (battle is not null && preferences.GuardianFormsEnabled != session.State.GameplayPreferences.GuardianFormsEnabled)
        {
            throw new InvalidOperationException("GF Transformations cannot change until the battle ends.");
        }
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
        BattleSession active = CreateLiveBattleSession(definition with { GuardianFormsEnabled = preferences.GuardianFormsEnabled, GuardianFormResources = preferences.GuardianFormsEnabled ? definition.GuardianFormResources : [] }, preferences.AtbMode);
        active.SetGunbladeMode(preferences.GunbladeMode);
        return active;
    }
}
