using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Runtime;

/// <summary>An immutable observation of accepted original-edition owners, never a reconstructed action history.</summary>
public readonly record struct OriginalAchievementObservedEvent(AchievementEventId EventId, AchievementContextId ContextId, OriginalAchievementProgress Progress, AchievementOwner? Owner) : IAchievementEvent
{
    public static OriginalAchievementObservedEvent Create(OriginalAchievementProgress progress, AchievementOwner? owner)
    {
        owner?.Validate();
        string ownerKey = owner?.Key ?? "unbound";
        string version = progress.OmegaProof is null ? "v1" : "v2";
        string context = "original-state/" + version + "/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ownerKey)));
        // Version 1 hashes a fixed field sequence, so adding future projection properties cannot invalidate old receipts.
        object?[] values = [progress.GuardianForceMask, progress.MaximumSeeDRank, progress.MaximumCharacterLevel, progress.AllCards, progress.MaximumGil, progress.WeaponUpgraded, progress.AllBlueMagic, progress.AllAngeloTricks, progress.MagazineCollection, progress.CardWins, progress.Chocobo, progress.ObelLake, progress.TimberManiacs];
        if (progress.OmegaProof is not null)
        {
            values = [.. values, progress.OmegaProof];
        }
        string eventId = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values)));
        return new(new(eventId), new(context), progress, owner);
    }

    public void Validate()
    {
        if (this != Create(Progress, Owner))
        {
            throw new InvalidDataException("Original achievement observation differs from its immutable receipt.");
        }
    }
}

public sealed partial class GameSession
{
    /// <summary>Called by the player host after accepted gameplay; an active battle retains separate transaction ownership.</summary>
    public void ObserveOriginalAchievements(bool? nativeLanguage12 = null)
    {
        if (State.Scene == SceneKind.Title || State.Battle is not null || State.PendingBattleEncounter is not null)
        {
            return;
        }
        OriginalAchievementProgress progress = OriginalAchievementState.Observe(State, nativeLanguage12);
        State = StageOriginalAchievementStateObservation(State, progress);
    }

    private GameState StageOriginalAchievementStateObservation(GameState candidate, OriginalAchievementProgress progress)
    {
        if (progress == default)
        {
            return candidate;
        }
        OriginalAchievementObservedEvent fact = OriginalAchievementObservedEvent.Create(progress, CaptureAchievementOwner?.Invoke());
        if (candidate.LastOriginalAchievementObservation == fact)
        {
            return candidate;
        }
        return candidate with
        {
            LastOriginalAchievementObservation = fact,
            PendingOriginalAchievementObservations = candidate.PendingOriginalAchievementObservations.Any(item => item.ContextId == fact.ContextId && item.EventId == fact.EventId) ? candidate.PendingOriginalAchievementObservations : candidate.PendingOriginalAchievementObservations.Add(fact)
        };
    }

    public void AcknowledgeOriginalAchievementObservations(IReadOnlyList<OriginalAchievementObservedEvent> facts)
    {
        HashSet<OriginalAchievementObservedEvent> committed = facts.ToHashSet();
        State = State with { PendingOriginalAchievementObservations = [.. State.PendingOriginalAchievementObservations.Where(item => !committed.Contains(item))] };
    }

    private static void ValidateOriginalAchievementCheckpoint(GameState state)
    {
        ValidateOriginalAchievementActions(state);
        if (state.OriginalAchievementMutationSequence < 0 || state.OriginalAchievementMutationContext is null && state.OriginalAchievementMutationSequence != 0 || state.OriginalAchievementMutationContext is { } context && !Guid.TryParseExact(context, "N", out _))
        {
            throw new InvalidDataException("Original achievement mutations require their saved context and nonnegative sequence.");
        }
        if (state.PendingOriginalAchievementObservations.IsDefault || state.PendingOriginalAchievementObservations.Select(item => (item.ContextId, item.EventId)).Distinct().Count() != state.PendingOriginalAchievementObservations.Length)
        {
            throw new InvalidDataException("Original achievement outbox contains duplicate or missing observations.");
        }
        foreach (OriginalAchievementObservedEvent fact in state.PendingOriginalAchievementObservations)
        {
            fact.Validate();
        }
        state.LastOriginalAchievementObservation?.Validate();
    }
}
