using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    public void ConfigureGameplayPreferences(GameplayPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (session.State.GameplayPreferences.Difficulty != BattleDifficulty.Hardcore)
        {
            preferences = preferences.NormalizeForDifficulty();
        }
        if (battle is not null && preferences.DrawOncePerSpell != session.State.GameplayPreferences.DrawOncePerSpell)
        {
            throw new InvalidOperationException("Draw once per spell cannot change until the battle ends.");
        }
        if (battle is not null && preferences.LimitOncePerTurn != session.State.GameplayPreferences.LimitOncePerTurn)
        {
            throw new InvalidOperationException("Limit once per turn cannot change until the battle ends.");
        }
        if (battle is not null && preferences.GuardianFormsEnabled != session.State.GameplayPreferences.GuardianFormsEnabled)
        {
            throw new InvalidOperationException("GF Transformations cannot change until the battle ends.");
        }
        session.SetGameplayPreferences(preferences);
        if (battle is not null)
        {
            battle.SetDifficulty(preferences.Difficulty);
            battle.SetMode(preferences.AtbMode);
            battle.SetGunbladeMode(preferences.GunbladeMode);
            session.SetBattle(battle.Capture());
        }
    }

    internal static BattleSession CreateLiveBattleSession(BattleDefinition definition, GameplayPreferences preferences)
    {
        preferences.Validate();
        BattleSession active = CreateLiveBattleSession(definition with { LimitOncePerTurn = preferences.LimitOncePerTurn, DrawOncePerSpell = preferences.DrawOncePerSpell, GuardianFormsEnabled = preferences.GuardianFormsEnabled, GuardianFormResources = preferences.GuardianFormsEnabled ? definition.GuardianFormResources : [] }, preferences.AtbMode);
        active.SetDifficulty(preferences.Difficulty);
        active.SetGunbladeMode(preferences.GunbladeMode);
        return active;
    }
}
