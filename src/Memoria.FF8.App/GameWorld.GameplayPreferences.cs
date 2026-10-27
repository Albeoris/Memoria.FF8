using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly Func<GameplayTuningPolicy>? readGameplayPolicy;
    private GameplayTuningPolicy GameplayPolicy => readGameplayPolicy?.Invoke() ?? GameplayTuningPolicy.Empty;

    public GameplayTuning ReadGameplayTuning() => (session.State.PendingGameplayPreferences ?? session.State.GameplayPreferences).Tuning;

    public string? GameplayTuningUnavailableReason(string field) => GameplayPolicy.UnavailableReason(field) ?? session.MagicMutationUnavailableReason;

    public string GameplayTuningStatus => session.State.PendingGameplayPreferences is not null ? "Changes take effect after the current battle." : "Changes apply to this playthrough.";

    public void EditGameplayTuning(GameplayTuning submitted)
    {
        GameplayTuning configured = session.State.ConfiguredGameplayTuning ?? session.State.GameplayPreferences.Tuning;
        GameplayTuningPolicy policy = GameplayPolicy;
        GameplayTuning next = policy.EditConfigured(configured, submitted);
        session.ConfigureGameplayTuning(next, policy.Apply(next));
    }

    public void SynchronizeGameplayTuning()
    {
        GameplayTuning configured = session.State.ConfiguredGameplayTuning ?? session.State.GameplayPreferences.Tuning;
        GameplayTuning effective = GameplayPolicy.Apply(configured);
        if (ReadGameplayTuning() == effective || session.MagicMutationUnavailableReason is not null)
        {
            return;
        }
        session.ConfigureGameplayTuning(configured, effective);
    }

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
        if (battle is not null && preferences.SelphieCardSlots != session.State.GameplayPreferences.SelphieCardSlots)
        {
            throw new InvalidOperationException("Selphie card Slots cannot change until the battle ends.");
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
            session.SetLiveBattle(battle);
        }
    }

    internal static BattleSession CreateLiveBattleSession(BattleDefinition definition, GameplayPreferences preferences)
    {
        preferences.Validate();
        BattleSession active = CreateLiveBattleSession(definition with { LimitOncePerTurn = preferences.LimitOncePerTurn, SelphieCardSlots = preferences.SelphieCardSlots, DrawOncePerSpell = preferences.DrawOncePerSpell, GuardianFormsEnabled = preferences.GuardianFormsEnabled, Tuning = preferences.Tuning, GuardianFormResources = preferences.GuardianFormsEnabled ? definition.GuardianFormResources : [] }, preferences.AtbMode);
        active.SetDifficulty(preferences.Difficulty);
        active.SetGunbladeMode(preferences.GunbladeMode);
        return active;
    }
}
