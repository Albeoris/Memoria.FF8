using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void ConfigureGameplayTuning(GameplayTuning configured, GameplayTuning effective)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(effective);
        configured.Validate();
        effective.Validate();
        GameplayPreferences preferences = State.PendingGameplayPreferences ?? State.GameplayPreferences;
        SetGameplayPreferences(preferences with { Tuning = effective });
        GameplayTuning current = State.GameplayPreferences.Tuning with { UnrestrictedScanRotation = effective.UnrestrictedScanRotation };
        BattleSnapshot? snapshot = State.Battle;
        if (snapshot is not null)
        {
            snapshot = snapshot with { Definition = snapshot.Definition with { Tuning = snapshot.Definition.Tuning with { UnrestrictedScanRotation = effective.UnrestrictedScanRotation } } };
        }
        liveBattle?.SetUnrestrictedScanRotation(effective.UnrestrictedScanRotation);
        GameplayPreferences? pending = State.PendingGameplayPreferences;
        GameplayPreferences active = State.GameplayPreferences with { Tuning = current };
        if (pending == active)
        {
            pending = null;
        }
        State = State with { ConfiguredGameplayTuning = configured, GameplayPreferences = active, PendingGameplayPreferences = pending, Battle = snapshot };
    }

    public MagicModePreview PreviewGameplayPreferences(GameplayPreferences preferences)
    {
        preferences = PrepareGameplayPreferences(preferences);
        if (State.CharacterVitals is null)
        {
            throw new InvalidOperationException("Magic mode migration requires an initialized party.");
        }
        return MagicModeCoordinator.Preview(State.CharacterVitals, preferences);
    }

    public void SetGameplayPreferences(GameplayPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences = PrepareGameplayPreferences(preferences);
        RequireBattleGameplayPreferenceBoundary(preferences);
        if (HasActiveBattle || State.PendingBattleEncounter is not null)
        {
            State = State with { PendingGameplayPreferences = preferences, PendingMagicModeReview = preferences.SharedMagicStock != State.GameplayPreferences.SharedMagicStock };
            return;
        }
        if (State.CharacterVitals is null || !MagicRulesDiffer(preferences))
        {
            State = State with { GameplayPreferences = preferences, PendingGameplayPreferences = null, PendingMagicModeReview = false };
            return;
        }
        ApplyGameplayPreferences(PreviewGameplayPreferences(preferences));
    }

    public void ApplyGameplayPreferences(MagicModePreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        _ = PrepareGameplayPreferences(preview.Preferences);
        preview.Preferences.Validate();
        RequireBattleGameplayPreferenceBoundary(preview.Preferences);
        if (HasActiveBattle || State.PendingBattleEncounter is not null)
        {
            State = State with { PendingGameplayPreferences = preview.Preferences, PendingMagicModeReview = preview.Preferences.SharedMagicStock != State.GameplayPreferences.SharedMagicStock };
            return;
        }
        if (preview.Source is null || State.CharacterVitals is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(preview.Source), JsonSerializer.SerializeToElement(State.CharacterVitals)))
        {
            throw new InvalidOperationException("The magic migration preview is stale; preview the current party again.");
        }
        if (!MagicRulesDiffer(preview.Preferences))
        {
            State = State with { GameplayPreferences = preview.Preferences, PendingGameplayPreferences = null, PendingMagicModeReview = false };
            return;
        }
        RequireMagicMutationBoundary();
        MagicModePreview verified = MagicModeCoordinator.Preview(State.CharacterVitals, preview.Preferences);
        PublishMagicVitals(verified.Vitals);
        State = State with { GameplayPreferences = verified.Preferences, PendingGameplayPreferences = null, PendingMagicModeReview = false };
    }

    public void ApplyPendingGameplayPreferences()
    {
        if (State.PendingGameplayPreferences is { } pending && !HasActiveBattle && State.PendingBattleEncounter is null && !State.PendingMagicModeReview)
        {
            SetGameplayPreferences(pending);
        }
    }

    public void SetAttackMode(int row, MagicAttackMode mode)
    {
        if ((uint)row >= 8 || !Enum.IsDefined(mode) || State.CharacterVitals is not { } vitals || !vitals.MagicSystem.SmartAttack || !State.GameplayPreferences.SmartAttack || HasActiveBattle)
        {
            throw new InvalidOperationException("Attack modes require an available field party record.");
        }
        RequireMagicMutationBoundary();
        MagicSystemState magic = vitals.MagicSystem.DeepCopy();
        magic.AttackModes[row] = mode;
        PublishMagicVitals(vitals with { MagicSystem = magic });
    }

    public void CancelPendingGameplayPreferences()
    {
        State = State with { PendingGameplayPreferences = null, PendingMagicModeReview = false };
    }

    private GameplayPreferences PrepareGameplayPreferences(GameplayPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (State.GameplayPreferences.Difficulty == BattleDifficulty.Hardcore || State.PendingGameplayPreferences?.Difficulty == BattleDifficulty.Hardcore)
        {
            if (preferences.Difficulty != BattleDifficulty.Hardcore || GameplayPreferences.NewGameOptions.Any(option => !option.IsEnabled(preferences)))
            {
                throw new InvalidOperationException("Hardcore cannot be disabled and its gameplay rules must remain enabled.");
            }
        }
        preferences = preferences.NormalizeForDifficulty();
        preferences.Validate();
        return preferences;
    }

    private void RequireBattleGameplayPreferenceBoundary(GameplayPreferences preferences)
    {
        if (State.TripleTriadPlay is not null && preferences.TripleTriad != State.GameplayPreferences.TripleTriad)
        {
            throw new InvalidOperationException("Triple Triad settings cannot change until the match ends.");
        }
        if (preferences.DrawOncePerSpell != State.GameplayPreferences.DrawOncePerSpell && (HasActiveBattle || State.PendingBattleEncounter is not null))
        {
            throw new InvalidOperationException("Draw once per spell cannot change until the battle ends.");
        }
        if (preferences.LimitOncePerTurn != State.GameplayPreferences.LimitOncePerTurn && (HasActiveBattle || State.PendingBattleEncounter is not null))
        {
            throw new InvalidOperationException("Limit once per turn cannot change until the battle ends.");
        }
        if (preferences.SelphieCardSlots != State.GameplayPreferences.SelphieCardSlots && (HasActiveBattle || State.PendingBattleEncounter is not null))
        {
            throw new InvalidOperationException("Selphie card Slots cannot change until the battle ends.");
        }
        if (preferences.GuardianFormsEnabled != State.GameplayPreferences.GuardianFormsEnabled && (HasActiveBattle || State.PendingBattleEncounter is not null))
        {
            throw new InvalidOperationException("GF Transformations cannot change until the battle ends.");
        }
    }

    private bool MagicRulesDiffer(GameplayPreferences preferences)
    {
        GameplayPreferences current = State.GameplayPreferences;
        return preferences.SharedMagicStock != current.SharedMagicStock || preferences.InfiniteMagic != current.InfiniteMagic || preferences.SmartAttack != current.SmartAttack || preferences.Tuning.UnlimitedMagic != current.Tuning.UnlimitedMagic;
    }

    public string? MagicMutationUnavailableReason => ActiveFieldMutationBoundary switch
    {
        FieldMutationBoundary.Transition => "Magic rules cannot change during a field transition.",
        FieldMutationBoundary.Party => "Magic rules cannot change during a scripted party transition.",
        FieldMutationBoundary.Tutorial => "Magic rules cannot change during a tutorial.",
        _ => null
    };

    private void RequireMagicMutationBoundary()
    {
        if (MagicMutationUnavailableReason is { } reason)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private void PublishMagicVitals(NativeCharacterVitals vitals)
    {
        vitals.Validate();
        static JsonElement? Update(JsonElement? checkpoint, NativeCharacterVitals current)
        {
            if (checkpoint is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty("CharacterVitals", out _))
            {
                return checkpoint;
            }
            JsonObject json = JsonNode.Parse(element.GetRawText())!.AsObject();
            json["CharacterVitals"] = JsonSerializer.SerializeToNode(current);
            return JsonSerializer.SerializeToElement(json);
        }
        State = State with { CharacterVitals = vitals.DeepCopy(), EventState = Update(State.EventState, vitals), DepartingFieldState = Update(State.DepartingFieldState, vitals), SavedBattleFlags = SynchronizeSavedBattleFlags(vitals) };
    }
}
