using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Security.Cryptography;

namespace Memoria.FF8;

public static class NativeJoinMovementAdapter
{
    public static bool Supports(FieldAssets field, SceneEventState scene, ActorState actor)
    {
        return IsSavedPartyBinding(field, scene, actor.ObjectId) && actor.NativeMotionAuthority is { Version: 1 } && actor.ModelId >= 0 && actor.ModelId < field.Models.Count && actor.PushRadius is >= 0 and <= ushort.MaxValue;
    }

    public static NativeJoinMovementResult? Resolve(FieldAssets field, SceneEventState scene, NativeJoinMovementQuery query)
    {
        if (!IsSavedPartyBinding(field, scene, query.ObjectId) || query.Direction is < 0 or > byte.MaxValue || query.Radius is < 0 or > ushort.MaxValue || query.Speed is not (2696 or 7929) || query.HasActorCollision is null)
        {
            return null;
        }
        FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, (byte)query.Direction, query.Speed, (ushort)query.Radius, false, true, 0, true);
        bool HasCollision(FieldNativeGroundProbe probe) => query.HasActorCollision(new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value));
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, scene.LockedTriangles, HasCollision);
        if (result.Status == FieldNativeMovementStatus.Unsupported || result.Proposals.Count is < 1 or > 16 || result.Proposals.Any(proposal => proposal.FanProbes.Count != 3 || proposal.FanProbes.Any(probe => !probe.Supported || probe.NativeHeight is null)))
        {
            return null;
        }
        NativeJoinMovementProposal[] proposals = result.Proposals.Select(proposal => new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction)).ToArray();
        IReadOnlyList<NativePlayerGroundProbe>[] probes = result.Proposals.Select(proposal => (IReadOnlyList<NativePlayerGroundProbe>)proposal.FanProbes.Select(probe => new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value, probe.ActorCollision)).ToArray()).ToArray();
        return new NativeJoinMovementResult(result.Status == FieldNativeMovementStatus.Accepted, result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, proposals, probes);
    }

    private static bool IsSavedPartyBinding(FieldAssets field, SceneEventState scene, int objectId)
    {
        return scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } initialization && initialization.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm))) && scene.NativeFieldLoadVersion == 1 && scene.NativePartyTraceVersion == 1 && scene.NativeActorLayoutVersion == 1 && scene.PartyBindingMemory is { Version: 1 } memory && Enumerable.Range(0, 3).Select(memory.At).Any(index => index != byte.MaxValue && index < scene.NativeActorObjectIds.Count && scene.NativeActorObjectIds[index] == objectId);
    }
}
