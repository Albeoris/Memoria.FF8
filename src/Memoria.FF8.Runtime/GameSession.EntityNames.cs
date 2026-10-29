namespace Memoria.FF8.Runtime;

/// <summary>A saved required naming request after its originating acquisition or item consumption.</summary>
public sealed record PendingEntityName(int Subject, string DefaultName, string Source)
{
    public string? Draft { get; init; }
}

public sealed partial class GameSession
{
    public void SetEntityNameDraft(string draft)
    {
        if (!GameNames.IsValidDraft(draft) || State.PendingEntityNames is not { Length: > 0 } pending)
        {
            throw new ArgumentException("An entity name draft requires a current request and valid Unicode text.", nameof(draft));
        }
        PendingEntityName[] next = pending.ToArray();
        next[0] = next[0] with { Draft = draft };
        Change(State with { PendingEntityNames = next }, advancePlaytime: false);
    }

    public void EnqueueEntityNames(IEnumerable<PendingEntityName> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        PendingEntityName[] added = names.ToArray();
        if (added.Length == 0)
        {
            return;
        }
        PendingEntityName[] pending = [.. State.PendingEntityNames ?? [], .. added];
        Change(State with { PendingEntityNames = pending.Length == 0 ? null : pending }, advancePlaytime: false);
    }

    public void CompleteEntityName(PendingEntityName expected)
    {
        if (State.PendingEntityNames is not { Length: > 0 } pending || pending[0] != expected)
        {
            throw new InvalidOperationException("Naming completion does not match the current saved request.");
        }
        Change(State with { PendingEntityNames = pending.Length == 1 ? null : pending[1..] }, advancePlaytime: false);
    }

    public void BeginItemEntityName(NativeEconomy economy, PendingEntityName request)
    {
        economy.Validate();
        if (State.PendingEntityNames is { Length: > 0 } || State.Economy is null || economy.InitOutSha256 != State.Economy.InitOutSha256)
        {
            throw new InvalidOperationException("Item naming requires the current inventory and no earlier required name request.");
        }
        ValidateEconomyAliases(State.EventMemory);
        GameState candidate = State with { Economy = economy.DeepCopy(), PendingEntityNames = [request] };
        // The field VM reads the process-owned economy on Capture, like other menu publications.
        ValidateEntityNames(candidate);
        ValidateLiveEventMemoryOwners(candidate, candidate.EventMemory);
        State = candidate;
    }

    private static void ValidateEntityNames(GameState state)
    {
        if (state.PendingEntityNames is not { } pending)
        {
            return;
        }
        if (pending.Length == 0 || state.Scene is not (SceneKind.Field or SceneKind.WorldMap))
        {
            throw new InvalidDataException("Required entity names need a nonempty queue in a field or world scene.");
        }
        foreach (PendingEntityName request in pending)
        {
            if (request is null || !GameNames.IsValidSubject(request.Subject) || !GameNames.IsValidDraft(request.DefaultName) || request.Draft is not null && !GameNames.IsValidDraft(request.Draft) || string.IsNullOrWhiteSpace(request.Source) || !(request.Source.StartsWith("item:", StringComparison.Ordinal) || request.Source.StartsWith("battle:", StringComparison.Ordinal)))
            {
                throw new InvalidDataException("A saved entity name request lacks its valid subject, name or source.");
            }
            if (request.Subject is >= 16 and <= 31 && (state.CharacterVitals is null || (state.CharacterVitals.GuardianForces[request.Subject - 16].Flags & 1) == 0))
            {
                throw new InvalidDataException("Required GF naming needs its already acquired canonical GF row.");
            }
            if (request.Source.StartsWith("item:", StringComparison.Ordinal))
            {
                string id = request.Source[5..];
                if (!int.TryParse(id, out int item) || item is < 1 or > 255 || item.ToString(System.Globalization.CultureInfo.InvariantCulture) != id || request.Subject is not (2 or GameNames.BokoSubject or >= 16 and <= 31))
                {
                    throw new InvalidDataException("An item name request requires its exact item BYTE and an original item naming subject.");
                }
            }
            else if (request.Subject is < 16 or > 31 || !state.AppliedBattleResultIds.Contains(request.Source[7..], StringComparer.Ordinal))
            {
                throw new InvalidDataException("A battle name request requires its acquired GF subject and committed result receipt.");
            }
        }
    }
}
