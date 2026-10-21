using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public MagicModePreview PreviewGameplayPreferences(GameplayPreferences preferences)
    {
        if (State.CharacterVitals is null)
        {
            throw new InvalidOperationException("Magic mode migration requires an initialized party.");
        }
        return MagicModeCoordinator.Preview(State.CharacterVitals, preferences);
    }

    public void SetGameplayPreferences(GameplayPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.Validate();
        RequireGuardianFormsPreferenceBoundary(preferences);
        if (State.Battle is not null || State.PendingBattleEncounter is not null)
        {
            State = State with { PendingGameplayPreferences = preferences, PendingMagicModeReview = preferences.SharedMagicStock != State.GameplayPreferences.SharedMagicStock };
            return;
        }
        if (State.CharacterVitals is null)
        {
            State = State with { GameplayPreferences = preferences };
            return;
        }
        ApplyGameplayPreferences(PreviewGameplayPreferences(preferences));
    }

    public void ApplyGameplayPreferences(MagicModePreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        RequireGuardianFormsPreferenceBoundary(preview.Preferences);
        if (State.Battle is not null || State.PendingBattleEncounter is not null)
        {
            State = State with { PendingGameplayPreferences = preview.Preferences, PendingMagicModeReview = preview.Preferences.SharedMagicStock != State.GameplayPreferences.SharedMagicStock };
            return;
        }
        if (preview.Source is null || State.CharacterVitals is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(preview.Source), JsonSerializer.SerializeToElement(State.CharacterVitals)))
        {
            throw new InvalidOperationException("The magic migration preview is stale; preview the current party again.");
        }
        RequireMagicMutationBoundary();
        MagicModePreview verified = MagicModeCoordinator.Preview(State.CharacterVitals, preview.Preferences);
        PublishMagicVitals(verified.Vitals);
        State = State with { GameplayPreferences = verified.Preferences, PendingGameplayPreferences = null, PendingMagicModeReview = false };
    }

    public void ApplyPendingGameplayPreferences()
    {
        if (State.PendingGameplayPreferences is { } pending && State.Battle is null && State.PendingBattleEncounter is null && !State.PendingMagicModeReview)
        {
            SetGameplayPreferences(pending);
        }
    }

    public void SetAttackMode(int row, MagicAttackMode mode)
    {
        if ((uint)row >= 8 || !Enum.IsDefined(mode) || State.CharacterVitals is not { } vitals || !vitals.MagicSystem.SmartAttack || !State.GameplayPreferences.SmartAttack || State.Battle is not null)
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

    private void RequireGuardianFormsPreferenceBoundary(GameplayPreferences preferences)
    {
        if (preferences.GuardianFormsEnabled != State.GameplayPreferences.GuardianFormsEnabled && (State.Battle is not null || State.PendingBattleEncounter is not null))
        {
            throw new InvalidOperationException("GF Transformations cannot change until the battle ends.");
        }
    }

    private void RequireMagicMutationBoundary()
    {
        static bool Present(JsonElement checkpoint, string name) => checkpoint.TryGetProperty(name, out JsonElement value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
        if (State.EventState is { ValueKind: JsonValueKind.Object } checkpoint && (checkpoint.TryGetProperty("TransitionRequested", out JsonElement transition) && transition.ValueKind == JsonValueKind.True || Present(checkpoint, "PartyJoin") || Present(checkpoint, "PartySplit") || checkpoint.TryGetProperty("Scene", out JsonElement scene) && scene.ValueKind == JsonValueKind.Object && (Present(scene, "NativeTutorial") || Present(scene, "NativeMenuTutorial"))))
        {
            throw new InvalidOperationException("Magic rules cannot change during tutorial or scripted party transitions.");
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
