using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

public static class NativePlayerMovementAdapter
{
    public static bool Supports(FieldAssets field, SceneEventState scene, ActorState actor)
    {
        return field.Id >= 72 && SupportsBinding(scene, actor.ObjectId) && (uint)actor.ModelId < field.Models.Count && actor.PushRadius is >= 0 and <= ushort.MaxValue;
    }

    public static NativePlayerMovementResult? Resolve(FieldAssets field, SceneEventState scene, NativePlayerMovementQuery query)
    {
        if (field.Id < 72 || !SupportsBinding(scene, query.ObjectId) || query.Direction is < 0 or > byte.MaxValue || query.EarlyRetryState is null or > 1 || query.EarlyRetryState != scene.NativePlayerEarlyRetryState || query.Radius is < 0 or > ushort.MaxValue || query.Speed is not (2696 or 7929))
        {
            return null;
        }
        FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, (byte)query.Direction, query.Speed, (ushort)query.Radius, true, false, query.EarlyRetryState.Value, true);
        bool HasCollision(FieldNativeGroundProbe probe)
        {
            return query.HasActorCollision?.Invoke(new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value)) == true;
        }
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, scene.LockedTriangles, HasCollision);
        bool blocked = result.Status == FieldNativeMovementStatus.Blocked;
        int maximumAttempts = request.EarlyRetryState == 1 ? 2 : 16;
        if (result.Status != FieldNativeMovementStatus.Accepted && !blocked || result.Proposals.Count < 1 || result.Proposals.Count > maximumAttempts || result.RetryCount != result.Proposals.Count + (result.RetryCount > maximumAttempts ? 1 : 0) || result.Proposals[0].Direction != query.Direction || result.Status == FieldNativeMovementStatus.Accepted && result.Direction != result.Proposals[^1].Direction || result.EarlyRetryState != 0 || result.Proposals.Any(proposal => proposal.FanProbes.Count != 3 || proposal.FanProbes.Any(probe => !probe.Supported || probe.NativeHeight is null)))
        {
            return null;
        }
        NativeJoinMovementProposal[] proposals = result.Proposals.Select(proposal => new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction)).ToArray();
        IReadOnlyList<NativePlayerGroundProbe>[] probes = result.Proposals.Select(proposal => (IReadOnlyList<NativePlayerGroundProbe>)proposal.FanProbes.Select(probe => new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value, probe.ActorCollision)).ToArray()).ToArray();
        return new NativePlayerMovementResult(result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, proposals, ProposalGroundProbes: probes, Accepted: !blocked);
    }

    private static bool SupportsBinding(SceneEventState scene, int objectId)
    {
        return scene.NativeFieldLoadVersion == 1 && scene.NativePartyTraceVersion == 1 && NativeFieldPartyBinding.TryGetObjectId(scene, 0, out int leaderObjectId) && leaderObjectId == objectId;
    }
}
