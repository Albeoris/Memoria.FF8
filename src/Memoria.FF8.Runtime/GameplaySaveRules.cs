using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record GameplaySaveInputOptions([property: JsonRequired] uint Options, [property: JsonRequired] ImmutableArray<byte> ButtonMap, [property: JsonRequired] byte? BattleMessageSpeed, [property: JsonRequired] byte? BattleSpeed, [property: JsonRequired] byte? FieldMessageSpeed, [property: JsonRequired] byte? SoundVolume);
public sealed record GameplaySaveEncounterProgress([property: JsonRequired] bool Enabled, [property: JsonRequired] NativeRandomEncounterState Progress);

public sealed record GameplaySaveRules([property: JsonRequired] GameplayPreferences Preferences, [property: JsonRequired] GameplayTuning? ConfiguredTuning, [property: JsonRequired] GameplayPreferences? PendingPreferences, [property: JsonRequired] bool PendingMagicModeReview, [property: JsonRequired] PendingEntityName[]? PendingNames, [property: JsonRequired] GameplaySaveInputOptions? Input, [property: JsonRequired] bool? FullBattlePlaythrough, [property: JsonRequired] GameplaySaveEncounterProgress? Encounters, [property: JsonRequired] DialogueDraft? DialogueChoice)
{
    internal static GameplaySaveRules Capture(GameState state)
    {
        state.InputConfiguration?.Validate();
        state.RandomEncounters?.Validate();
        return new(state.GameplayPreferences, state.ConfiguredGameplayTuning, state.PendingGameplayPreferences, state.PendingMagicModeReview, state.PendingEntityNames, state.InputConfiguration is { } input ? new(input.Options, input.ButtonMap, input.BattleMessageSpeed, input.NativeBattleSpeedByte, input.FieldMessageSpeedByte, input.SoundVolume) : null, state.FullBattlePlaythrough, state.RandomEncounters is { } encounters ? new(encounters.Enabled, encounters.Native) : null, state.DialogueDraft);
    }

    public void Validate()
    {
        if (Preferences is null || PendingNames is { Length: 0 } || PendingNames?.Any(request => request is null || !GameNames.IsValidSubject(request.Subject) || !GameNames.IsValidDraft(request.DefaultName) || request.Draft is not null && !GameNames.IsValidDraft(request.Draft) || string.IsNullOrWhiteSpace(request.Source)) == true || Encounters is { Progress: null } || DialogueChoice is { } draft && !GameNames.IsValidDraft(draft.EnteredName))
        {
            throw new InvalidDataException("Gameplay rule choices are malformed.");
        }
        Preferences.Validate();
        ConfiguredTuning?.Validate();
        PendingPreferences?.Validate();
        Encounters?.Progress.Validate();
        if (Input is { } input)
        {
            new NativeInputConfiguration(NativeInputConfiguration.OriginalInitHash, input.Options, input.ButtonMap).Validate();
        }
    }
}
