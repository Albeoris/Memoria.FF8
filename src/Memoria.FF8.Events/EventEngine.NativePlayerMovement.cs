using System.Numerics;

namespace Memoria.FF8.Events;

public sealed record NativePlayerMovementQuery(int ObjectId, int Triangle, int FixedX, int FixedY, int FixedZ, int Direction, short Speed, int Radius, byte? EarlyRetryState = null, Func<NativePlayerGroundProbe, bool>? HasActorCollision = null);
public sealed record NativePlayerGroundProbe(int FixedX, int FixedY, int NativeHeight, bool ActorCollision = false);
public sealed record NativePlayerMovementResult(int FixedX, int FixedY, int FixedZ, int Triangle, int Direction, IReadOnlyList<NativeJoinMovementProposal> Proposals, IReadOnlyList<NativePlayerGroundProbe>? GroundProbes = null, IReadOnlyList<IReadOnlyList<NativePlayerGroundProbe>>? ProposalGroundProbes = null, bool Accepted = true);

public sealed partial class EventEngine
{
    public Func<NativePlayerMovementQuery, NativePlayerMovementResult?>? ResolveNativePlayerMovement { get; set; }
    public Func<ActorState, bool>? CanUseNativePlayerMovement { get; set; }

    private bool IsNativePartyPlayerBody(ActorState actor)
    {
        if (Scene.NativeActorLayoutVersion == 1 && Scene.PartyBindingMemory is { Version: 1 } memory)
        {
            byte retained = memory.First;
            return retained != 255 && retained < Scene.NativeActorObjectIds.Count && (!variables.TryGetValue(194, out int alias) || alias == retained) && Scene.NativeActorObjectIds[retained] == actor.ObjectId;
        }
        return ResolvePartyActor(0)?.ObjectId == actor.ObjectId;
    }

    private void RecordNativeScriptPlacement(ActorState actor)
    {
        if (Scene.NativePartyTraceVersion != 1 || !IsNativePartyPlayerBody(actor))
        {
            return;
        }
        // SET3 writes the native fixed coordinates directly. Retain this executed
        // write before the next ordinary step; never infer it from a saved float.
        if (actor.X is < short.MinValue or > short.MaxValue || actor.Y is < short.MinValue or > short.MaxValue || actor.Z is < short.MinValue or > short.MaxValue || actor.X != MathF.Truncate(actor.X) || actor.Y != MathF.Truncate(actor.Y) || actor.Z != MathF.Truncate(actor.Z))
        {
            throw new NotSupportedException("Native player SET3 placement exceeds its signed coordinate contract.");
        }
        Scene.NativePlayerPosition = new FieldFixedPosition((int)actor.X * 4096, (int)actor.Y * 4096, (int)actor.Z * 4096);
    }

    private (WalkmeshPosition Position, FieldFixedPosition FixedPosition, int Direction, int[] PushContacts, bool NativeMovement, FieldFixedPosition? TriggerPosition) ResolveCertifiedNativePlayerStep(ActorState player, FieldFixedPosition current, int direction, int speed)
    {
        if (ResolveNativePlayerMovement is null || CanUseNativePlayerMovement?.Invoke(player) != true)
        {
            throw new NotSupportedException("Persisted native player movement requires its certified bounded-attempt adapter; original state preserved.");
        }
        byte? earlyRetry = Scene.NativePlayerEarlyRetryState;
        NativePlayerMovementQuery query = new(player.ObjectId, player.WalkmeshTriangle, current.X, current.Y, current.Z, direction, (short)speed, player.PushRadius, earlyRetry, probe => EvaluateNativePlayerGroundContacts(player, probe).Collision);
        NativePlayerMovementResult movement = ResolveNativePlayerMovement(query) ?? throw new NotSupportedException("Native player movement requires a certified original retry-state result; unproved retry/contact chains preserve original state.");
        bool blocked = !movement.Accepted && (earlyRetry is 0 or 1 || FieldId == 229 && Scene.NativeFieldLoadVersion == 1);
        int maximumAttempts = earlyRetry == 0 || blocked && earlyRetry is null ? 16 : 2;
        if (earlyRetry is > 1 || !movement.Accepted && !blocked || movement.Proposals is null || movement.Proposals.Count < 1 || movement.Proposals.Count > maximumAttempts || movement.Direction is < 0 or > byte.MaxValue || movement.Proposals[0].Direction != direction || movement.Accepted && movement.Proposals[^1].Direction != movement.Direction || movement.Accepted && (movement.FixedX != movement.Proposals[^1].FixedX || movement.FixedY != movement.Proposals[^1].FixedY) || blocked && (movement.FixedX != current.X || movement.FixedY != current.Y || movement.FixedZ != current.Z) || movement.Triangle is < 0 or > short.MaxValue || (movement.FixedX >> 12) is < short.MinValue or > short.MaxValue || (movement.FixedY >> 12) is < short.MinValue or > short.MaxValue || (movement.FixedZ >> 12) is < short.MinValue or > short.MaxValue || (movement.FixedZ & 4095) != 0)
        {
            throw new NotSupportedException("Native bounded-attempt adapter returned an inconsistent fixed proposal, direction or geometry result; original state preserved.");
        }
        for (int index = 0; index < movement.Proposals.Count; index++)
        {
            NativeJoinMovementProposal proposal = movement.Proposals[index];
            if (proposal.Direction is < 0 or > byte.MaxValue || (proposal.FixedX >> 12) is < short.MinValue or > short.MaxValue || (proposal.FixedY >> 12) is < short.MinValue or > short.MaxValue)
            {
                throw new NotSupportedException("Native player retry proposal exceeds its original coordinate or direction width; original state preserved.");
            }
            if (index > 0 && proposal.Direction != movement.Proposals[index - 1].Direction && proposal.Direction != ((movement.Proposals[index - 1].Direction + 8) & 255) && proposal.Direction != ((movement.Proposals[index - 1].Direction - 8) & 255))
            {
                throw new NotSupportedException("Native player retry changed direction outside the proved ground steering increments; original state preserved.");
            }
        }
        int lastDirection = movement.Proposals[^1].Direction;
        if (!movement.Accepted && movement.Direction != lastDirection && movement.Direction != ((lastDirection + 8) & 255) && movement.Direction != ((lastDirection - 8) & 255))
        {
            throw new NotSupportedException("Native blocked movement changed its final direction outside the last ground steering increment; original state preserved.");
        }
        if (earlyRetry is null && movement.Accepted && movement.Proposals.Count == 2 && movement.Direction != ((direction + 8) & 255) && movement.Direction != ((direction - 8) & 255))
        {
            throw new NotSupportedException("Native player second attempt needs one original eight-unit steering adjustment; original state preserved.");
        }
        ValidatePendingNativePushContacts();
        int[] pushContacts;
        if (movement.ProposalGroundProbes is not null)
        {
            if (movement.ProposalGroundProbes.Count != movement.Proposals.Count || movement.ProposalGroundProbes.Any(probes => probes is null) || movement.GroundProbes is not null)
            {
                throw new NotSupportedException("Native retry ground probes must match every ordered proposal without an ambiguous legacy triple; original state preserved.");
            }
            if (movement.ProposalGroundProbes.Any(probes => probes.Any(probe => probe.ActorCollision)) && (movement.Accepted || movement.ProposalGroundProbes.Take(movement.Proposals.Count - 1).Any(probes => probes.Any(probe => probe.ActorCollision))))
            {
                throw new NotSupportedException("Native ordinary player actor collision must stop at its first contacting proposal and reject XYZ; original state preserved.");
            }
            List<int> contacts = [];
            HashSet<int> seen = [];
            for (int index = 0; index < movement.Proposals.Count; index++)
            {
                foreach (int objectId in ValidateNativePlayerGroundContacts(player, movement.Proposals[index], movement.ProposalGroundProbes[index]))
                {
                    if (seen.Add(objectId))
                    {
                        contacts.Add(objectId);
                    }
                }
            }
            pushContacts = contacts.ToArray();
        }
        else if (movement.GroundProbes is not null && movement.Proposals.Count == 1)
        {
            if (movement.Accepted && movement.GroundProbes.Any(probe => probe.ActorCollision))
            {
                throw new NotSupportedException("Native ordinary player actor collision must reject XYZ; original state preserved.");
            }
            pushContacts = ValidateNativePlayerGroundContacts(player, movement.Proposals[0], movement.GroundProbes);
        }
        else
        {
            if (movement.Proposals.Count != 1 || movement.GroundProbes is not null || FieldId != 172 || movement.FixedZ != current.Z)
            {
                throw new NotSupportedException("Native player movement outside the historical172 flat contract requires all three original fan ground heights; original state preserved.");
            }
            pushContacts = EnsureNativeActorEnvelopeClear(player, movement.FixedX, movement.FixedY, current.Z, direction, true);
        }
        WalkmeshPosition position = new(new Vector3(movement.FixedX / 4096f, movement.FixedY / 4096f, movement.FixedZ / 4096f), movement.Triangle);
        PreparePartyTraceFollowers(position, movement.Direction, movement.FixedX != current.X || movement.FixedY != current.Y);
        NativeJoinMovementProposal trigger = movement.Proposals[^1];
        return (position, new FieldFixedPosition(movement.FixedX, movement.FixedY, movement.FixedZ), movement.Direction, pushContacts, true, new FieldFixedPosition(trigger.FixedX, trigger.FixedY, current.Z));
    }
}
