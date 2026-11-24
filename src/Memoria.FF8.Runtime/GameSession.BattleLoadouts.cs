using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private long battleLoadoutSessionEpoch;

    private string BattleLoadoutBaseline()
    {
        NativeCharacterVitals? vitals = State.CharacterVitals;
        return JsonSerializer.Serialize(new { SessionEpoch = battleLoadoutSessionEpoch, State.BattleLoadouts, Records = vitals?.Records.Select(record => new { record.CharacterId, record.Weapon, record.MemberFlags }), vitals?.DreamWeapons, vitals?.DreamEnabled, vitals?.ExtendedRosterEnabled, State.LimitProgress, State.SavedBattleFlags, State.CharacterAppearance });
    }

    public string? BattleLoadoutMutationUnavailableReason
    {
        get
        {
            if (State.Scene is not (SceneKind.Field or SceneKind.WorldMap) || State.CharacterVitals is null)
            {
                return "Current-game loadouts require an initialized field or world game.";
            }
            if (HasActiveBattle || State.PendingBattleEncounter is not null || State.RandomEncounters?.Pending is not null)
            {
                return "Loadouts cannot change during a battle or pending encounter.";
            }
            return ActiveFieldMutationBoundary switch
            {
                FieldMutationBoundary.Transition => "Loadouts cannot change during a field transition.",
                FieldMutationBoundary.Party => "Loadouts cannot change during a scripted party transition.",
                FieldMutationBoundary.Tutorial => "Loadouts cannot change during a tutorial.",
                _ => null
            };
        }
    }

    public BattleLoadoutDraft CreateBattleLoadoutDraft(IEnumerable<BattleLoadoutDefaults> defaults, string resourceRevision = "", IEnumerable<int>? initialWeapons = null)
    {
        return new(State.BattleLoadouts, defaults, resourceRevision, initialWeapons) { Owner = this, Baseline = BattleLoadoutBaseline() };
    }

    /// <summary>Validates the complete detached replacement before publishing a single owner-thread mutation.</summary>
    public void ApplyBattleLoadoutDraft(BattleLoadoutDraft draft, string resourceRevision = "", Action<int, BattleCharacterLoadoutOverride>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (BattleLoadoutMutationUnavailableReason is { } unavailable)
        {
            throw new InvalidOperationException(unavailable);
        }
        if (!ReferenceEquals(draft.Owner, this) || draft.Baseline != BattleLoadoutBaseline() || draft.ResourceRevision != resourceRevision)
        {
            throw new InvalidOperationException("The loadout draft is stale. Reopen it using the current game and resources.");
        }
        BattleLoadoutOverrides replacement = draft.Overrides;
        replacement.Validate();
        foreach ((int character, BattleCharacterLoadoutOverride entry) in replacement.Characters)
        {
            validate?.Invoke(character, entry);
        }
        if (draft.Baseline != BattleLoadoutBaseline())
        {
            throw new InvalidOperationException("The game changed while validating the loadout draft.");
        }
        State = State with { BattleLoadouts = replacement.Characters.Count == 0 && replacement.WeaponInstances is null ? null : replacement.DeepCopy() };
    }
}
