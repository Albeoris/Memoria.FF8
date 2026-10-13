using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

public static class NativePlayerMovementAdapter
{
    public static bool Supports(FieldAssets field, SceneEventState scene, ActorState actor)
    {
        return field.Id >= 72 && SupportsBinding(scene, actor.ObjectId) && (uint)actor.ModelId < field.Models.Count && actor.CharacterId == 0 && actor.PushRadius == 48;
    }

    public static NativePlayerMovementResult? Resolve(FieldAssets field, SceneEventState scene, NativePlayerMovementQuery query)
    {
        if (field.Id < 72 || !SupportsBinding(scene, query.ObjectId) || query.Direction is < 0 or > byte.MaxValue || query.EarlyRetryState is > 1 || query.EarlyRetryState != scene.NativePlayerEarlyRetryState || query.Radius != 48 || query.Speed is not (2696 or 7929))
        {
            return null;
        }
        // Known bytes come only from executed native loader/line writes. Legacy
        // unknown bytes still require agreement of both possible native paths.
        FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, (byte)query.Direction, query.Speed, (ushort)query.Radius, true, false, query.EarlyRetryState ?? 0, true);
        bool HasCollision(FieldNativeGroundProbe probe)
        {
            return query.HasActorCollision?.Invoke(new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value)) == true;
        }
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, scene.LockedTriangles, HasCollision);
        bool known = query.EarlyRetryState is not null;
        // TODO(TD-003): migrate unknown legacy retry state or prove shared blocked-result equivalence without the field229 exception
        bool blocked = result.Status == FieldNativeMovementStatus.Blocked && (known || field.Id == 229);
        int maximumAttempts = query.EarlyRetryState == 0 || blocked && !known ? 16 : 2;
        int cappedAttempts = request.EarlyRetryState == 1 ? 2 : 16;
        if (result.Status != FieldNativeMovementStatus.Accepted && !blocked || result.Proposals.Count < 1 || result.Proposals.Count > maximumAttempts || result.RetryCount != result.Proposals.Count + (result.RetryCount > cappedAttempts ? 1 : 0) || result.Proposals[0].Direction != query.Direction || result.Status == FieldNativeMovementStatus.Accepted && result.Direction != result.Proposals[^1].Direction || result.EarlyRetryState != 0 || result.Proposals.Any(proposal => proposal.FanProbes.Count != 3 || proposal.FanProbes.Any(probe => !probe.Supported || probe.NativeHeight is null)))
        {
            return null;
        }
        if (!known)
        {
            FieldNativeMovementResult alternate = FieldNativeMovementProbe.Resolve(field.Walkmesh, request with { EarlyRetryState = 1 }, scene.LockedTriangles, HasCollision);
            if (!ResultsAgree(result, alternate, blocked))
            {
                return null;
            }
        }
        NativeJoinMovementProposal[] proposals = result.Proposals.Select(proposal => new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction)).ToArray();
        IReadOnlyList<NativePlayerGroundProbe>[] probes = result.Proposals.Select(proposal => (IReadOnlyList<NativePlayerGroundProbe>)proposal.FanProbes.Select(probe => new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value, probe.ActorCollision)).ToArray()).ToArray();
        return new NativePlayerMovementResult(result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, proposals, ProposalGroundProbes: probes, Accepted: !blocked);
    }

    private static bool ResultsAgree(FieldNativeMovementResult first, FieldNativeMovementResult second, bool repeatedBlocked = false)
    {
        if (first.Status != second.Status || first.FixedX != second.FixedX || first.FixedY != second.FixedY || first.FixedZ != second.FixedZ || first.Triangle != second.Triangle || first.Direction != second.Direction || first.EarlyRetryState != second.EarlyRetryState || !repeatedBlocked && (first.RetryCount != second.RetryCount || first.Proposals.Count != second.Proposals.Count) || first.Factors != second.Factors || first.UnsupportedReason != second.UnsupportedReason || second.Proposals.Count == 0 || second.Proposals.Count > first.Proposals.Count || !ProbesAgree(first.CenterProbe, second.CenterProbe))
        {
            return false;
        }
        for (int index = 0; index < first.Proposals.Count; index++)
        {
            FieldNativeMovementProposal left = first.Proposals[index];
            FieldNativeMovementProposal right = second.Proposals[Math.Min(index, second.Proposals.Count - 1)];
            if (!repeatedBlocked && left.Attempt != right.Attempt || left.Direction != right.Direction || left.FixedX != right.FixedX || left.FixedY != right.FixedY || left.FanProbes.Count != right.FanProbes.Count)
            {
                return false;
            }
            for (int fan = 0; fan < left.FanProbes.Count; fan++)
            {
                if (!ProbesAgree(left.FanProbes[fan], right.FanProbes[fan]))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool ProbesAgree(FieldNativeGroundProbe? first, FieldNativeGroundProbe? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }
        return first.FixedX == second.FixedX && first.FixedY == second.FixedY && first.RadiusOffsetX == second.RadiusOffsetX && first.RadiusOffsetY == second.RadiusOffsetY && first.Triangle == second.Triangle && first.NativeHeight == second.NativeHeight && first.Steering == second.Steering && first.ActorCollision == second.ActorCollision && first.UnsupportedReason == second.UnsupportedReason && first.TraversedTriangles.SequenceEqual(second.TraversedTriangles);
    }

    private static bool SupportsBinding(SceneEventState scene, int objectId)
    {
        if (scene.NativeFieldLoadVersion != 1 || scene.NativeActorLayoutVersion != 1 || scene.NativePartyTraceVersion != 1 || scene.NativeActorObjectIds.Count is < 1 or > 255 || scene.NativeActorObjectIds.Any(id => id < 0) || scene.NativeActorObjectIds.Distinct().Count() != scene.NativeActorObjectIds.Count || scene.FieldPartyNativeActorIds.Count != 3 || scene.Party.Count is < 1 or > 3 || scene.Party[0] != 0 || scene.Party.Any(character => character is < 0 or >= 255) || scene.Party.Distinct().Count() != scene.Party.Count)
        {
            return false;
        }
        HashSet<int> present = [];
        for (int slot = 0; slot < 3; slot++)
        {
            int nativeActor = scene.FieldPartyNativeActorIds[slot];
            if (slot >= scene.Party.Count)
            {
                if (nativeActor != 255)
                {
                    return false;
                }
            }
            else if (nativeActor == 255)
            {
                // 478430 skips each absent runtime follower independently;
                // canonical party membership and saved B194..196 can remain.
                if (slot == 0)
                {
                    return false;
                }
            }
            else if ((uint)nativeActor >= scene.NativeActorObjectIds.Count || !present.Add(nativeActor))
            {
                return false;
            }
        }
        return scene.NativeActorObjectIds[scene.FieldPartyNativeActorIds[0]] == objectId;
    }
}
