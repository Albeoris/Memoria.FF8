using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Account delivery belongs to the local achievement profile, never a portable game save.</summary>
internal static class SaveAccountPrivacy
{
    internal static GameState Detach(GameState state)
    {
        return state with
        {
            EventState = DetachEventState(state.EventState),
            DepartingFieldState = DetachEventState(state.DepartingFieldState),
            WorldEventState = DetachEventState(state.WorldEventState),
            CharacterVitals = state.CharacterVitals is { } vitals ? vitals with { GuardianForceCompatibilityDiagnostics = [] } : null,
            PendingAchievementFacts = [.. state.PendingAchievementFacts.Select(fact => fact with { Owner = null })],
            PendingAchievementObservations = [.. state.PendingAchievementObservations.Select(fact => fact with { Owner = null })],
            PendingOriginalAchievementObservations = [.. state.PendingOriginalAchievementObservations.Select(Detach).Distinct()],
            PendingOriginalAchievementActions = [.. state.PendingOriginalAchievementActions.Select(fact => fact with { Owner = null })],
            LastOriginalAchievementObservation = state.LastOriginalAchievementObservation is { } observation ? Detach(observation) : null
        };
    }

    internal static GameplaySaveComponentDocument Detach(GameplaySaveComponentDocument document)
    {
        return document.Progression is { } progression ? document with { Progression = progression with { Transactions = Detach(progression.Transactions) } } : document;
    }

    internal static GameplaySaveTransactions Detach(GameplaySaveTransactions transactions)
    {
        return transactions with
        {
            PendingDefeats = [.. transactions.PendingDefeats.Select(fact => fact with { Owner = null })],
            PendingKillObservations = [.. transactions.PendingKillObservations.Select(fact => fact with { Owner = null })],
            PendingOriginalObservations = [.. transactions.PendingOriginalObservations.Select(Detach).Distinct()],
            PendingOriginalActions = [.. transactions.PendingOriginalActions.Select(fact => fact with { Owner = null })],
            LastOriginalObservation = transactions.LastOriginalObservation is { } observation ? Detach(observation) : null
        };
    }

    private static OriginalAchievementObservedEvent Detach(OriginalAchievementObservedEvent observation)
    {
        observation.Validate();
        // Context includes an account-derived hash, so regenerate the entire unbound receipt.
        return OriginalAchievementObservedEvent.Create(observation.Progress, null);
    }

    private static JsonElement? DetachEventState(JsonElement? state)
    {
        if (state is not { ValueKind: JsonValueKind.Object } value)
        {
            return state;
        }
        JsonObject saved = JsonNode.Parse(value.GetRawText())!.AsObject();
        if (saved["LastFault"] is JsonObject fault)
        {
            // Keep the blocked VM and native source coordinates, without exception text from the host.
            fault["Message"] = "Saved script fault.";
        }
        saved.Remove("FaultContext");
        saved.Remove("LatestFaultContext");
        if (saved["CharacterVitals"] is JsonObject vitals)
        {
            vitals["GuardianForceCompatibilityDiagnostics"] = new JsonArray();
        }
        if (saved["MovieSource"] is JsonObject movie)
        {
            foreach (string member in movie.Select(property => property.Key).Where(member => member is not (nameof(NativeMovieSource.DiscIndex) or nameof(NativeMovieSource.MovieIndex))).ToArray())
            {
                movie.Remove(member);
            }
        }
        return JsonSerializer.SerializeToElement(saved);
    }
}
