using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum EncounterHpCause
{
    Damage,
    Healing,
    HpCost,
    ScriptAssignment,
    DeathStatus
}

public sealed record EncounterHpIntent(int ContactId, string? SourceActorId, string TargetActorId, int Magnitude, EncounterHpCause Cause);
public sealed record EncounterDamageActor(string ActorId, int CurrentHp, bool Active);
public sealed record EncounterDamageRoute(string Id, string SourceActorId, ImmutableArray<string> RecipientActorIds);
public sealed record EncounterHpReceipt(int ContactId, string? SourceActorId, string OriginalTargetId, string RecipientId, int AssignedDamage, int AppliedLoss, string? RouteId);

/// <summary>Plans a simultaneous resolved-damage group before any HP owner is mutated.</summary>
public static class EncounterDamageRouting
{
    public static ImmutableArray<EncounterHpReceipt> Plan(IEnumerable<EncounterHpIntent> contacts, IEnumerable<EncounterDamageActor> actors, IEnumerable<EncounterDamageRoute> routes)
    {
        EncounterHpIntent[] intents = contacts.OrderBy(intent => intent.ContactId).ToArray();
        EncounterDamageActor[] participants = actors.ToArray();
        EncounterDamageRoute[] configured = routes.ToArray();
        if (intents.Any(intent => intent.ContactId < 0 || string.IsNullOrWhiteSpace(intent.TargetActorId) || intent.Magnitude < 0 || !Enum.IsDefined(intent.Cause)) || intents.Select(intent => intent.ContactId).Distinct().Count() != intents.Length)
        {
            throw new InvalidDataException("Resolved HP intents require unique contact identities, a target, a typed cause and a nonnegative magnitude.");
        }
        if (participants.Any(actor => string.IsNullOrWhiteSpace(actor.ActorId) || actor.CurrentHp < 0) || participants.Select(actor => actor.ActorId).Distinct(StringComparer.Ordinal).Count() != participants.Length)
        {
            throw new InvalidDataException("A damage transaction requires distinct allocation identities and nonnegative HP.");
        }
        Dictionary<string, EncounterDamageActor> snapshots = participants.ToDictionary(actor => actor.ActorId, StringComparer.Ordinal);
        if (configured.Any(route => string.IsNullOrWhiteSpace(route.Id) || !snapshots.ContainsKey(route.SourceActorId) || route.RecipientActorIds.IsDefaultOrEmpty || route.RecipientActorIds.Contains(route.SourceActorId) || route.RecipientActorIds.Any(id => !snapshots.ContainsKey(id)) || route.RecipientActorIds.Distinct(StringComparer.Ordinal).Count() != route.RecipientActorIds.Length) || configured.Select(route => route.SourceActorId).Distinct(StringComparer.Ordinal).Count() != configured.Length || configured.Select(route => route.Id).Distinct(StringComparer.Ordinal).Count() != configured.Length)
        {
            throw new InvalidDataException("Damage routes require unique ownership, existing recipients and no self-recipient.");
        }
        Dictionary<string, int> remaining = participants.ToDictionary(actor => actor.ActorId, actor => actor.CurrentHp, StringComparer.Ordinal);
        ImmutableArray<EncounterHpReceipt>.Builder receipts = ImmutableArray.CreateBuilder<EncounterHpReceipt>();
        foreach (EncounterHpIntent intent in intents)
        {
            if (!snapshots.TryGetValue(intent.TargetActorId, out EncounterDamageActor? target))
            {
                throw new InvalidDataException($"Contact {intent.ContactId} references unknown target '{intent.TargetActorId}'.");
            }
            if (intent.Cause != EncounterHpCause.Damage)
            {
                continue;
            }
            EncounterDamageRoute? route = configured.SingleOrDefault(candidate => candidate.SourceActorId == intent.TargetActorId);
            string[] recipients = route is null || intent.Magnitude == 0 ? [] : route.RecipientActorIds.Where(id => snapshots[id].Active && snapshots[id].CurrentHp > 0).Order(StringComparer.Ordinal).ToArray();
            if (recipients.Length == 0)
            {
                Add(intent.TargetActorId, intent.Magnitude, null);
            }
            else
            {
                receipts.Add(new(intent.ContactId, intent.SourceActorId, intent.TargetActorId, intent.TargetActorId, 0, 0, route!.Id));
                int share = intent.Magnitude / recipients.Length;
                foreach (string recipient in recipients)
                {
                    Add(recipient, share, route.Id);
                }
            }

            void Add(string recipientId, int assigned, string? routeId)
            {
                int applied = snapshots[recipientId].Active ? Math.Min(remaining[recipientId], assigned) : 0;
                remaining[recipientId] -= applied;
                receipts.Add(new(intent.ContactId, intent.SourceActorId, intent.TargetActorId, recipientId, assigned, applied, routeId));
            }
        }
        return receipts.ToImmutable();
    }
}
