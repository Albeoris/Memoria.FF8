using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Security.Cryptography;

namespace Memoria.FF8;

public static class NativeScriptMovementAdapter
{
    public static bool Supports(FieldAssets field, SceneEventState scene, ActorState actor)
    {
        return SupportsGenericBinding(field, scene, actor);
    }

    public static NativeScriptDirectionResult? ResolveDirection(FieldAssets field, SceneEventState scene, NativeScriptDirectionQuery query, ActorState? actor = null)
    {
        bool generic = actor is not null && actor.ObjectId == query.ObjectId && SupportsGenericBinding(field, scene, actor) && (actor.MovementDirectionLimit == query.DirectionLimit || query.HoldsFacing && query.DirectionLimit == 0);
        if (!generic || query.Radius != actor!.PushRadius || (query.TargetX >> 12) is < short.MinValue or > short.MaxValue || (query.TargetY >> 12) is < short.MinValue or > short.MaxValue)
        {
            return null;
        }
        FieldNativeScriptDirectionRequest request = new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit);
        FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(request);
        NativeScriptDirectionStatus? status = result.Status switch
        {
            FieldNativeScriptDirectionStatus.Moving => NativeScriptDirectionStatus.Moving,
            FieldNativeScriptDirectionStatus.Stopped => NativeScriptDirectionStatus.Stopped,
            FieldNativeScriptDirectionStatus.Snapped => NativeScriptDirectionStatus.Snapped,
            _ => null
        };
        return status is NativeScriptDirectionStatus supported ? new NativeScriptDirectionResult(supported, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator) : null;
    }

    public static NativeScriptMovementResult? ResolveGeometry(FieldAssets field, SceneEventState scene, NativeScriptMovementQuery query, ActorState? actor = null)
    {
        if (actor is null || actor.ObjectId != query.ObjectId || !SupportsGenericBinding(field, scene, actor) || query.Radius != actor.PushRadius || query.HasActorCollision is null)
        {
            return null;
        }
        // Events verifies every returned native fan against retained actor peers
        // before publishing this pure geometry result, including blocked attempts.
        FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, query.Direction, query.Speed, query.Radius, false, true, 0, true);
        bool HasCollision(FieldNativeGroundProbe probe) => query.HasActorCollision(new NativeScriptGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value));
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, scene.LockedTriangles, HasCollision);
        if (result.Status == FieldNativeMovementStatus.Unsupported || result.Proposals.Count is < 1 or > 16 || result.Proposals.Any(proposal => proposal.FanProbes.Count != 3 || proposal.FanProbes.Any(probe => !probe.Supported || probe.NativeHeight is null)))
        {
            return null;
        }
        NativeScriptMovementProposal[] proposals = result.Proposals.Select(proposal => new NativeScriptMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction, proposal.FanProbes.Select(probe => new NativeScriptGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value, probe.ActorCollision)).ToArray())).ToArray();
        NativeScriptMovementStatus status = result.Status == FieldNativeMovementStatus.Accepted ? NativeScriptMovementStatus.Accepted : NativeScriptMovementStatus.Blocked;
        return new NativeScriptMovementResult(status, result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, proposals);
    }

    private static bool SupportsGenericBinding(FieldAssets field, SceneEventState scene, ActorState actor)
    {
        string hash = Convert.ToHexString(SHA256.HashData(field.Jsm));
        bool allocated = scene.NativeFieldInitialization?.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, hash) == true;
        // Native4789A0 drives every retained mode1 body, including536=FFFF.
        // Empty resource532890 and actor-contact47A720 have their own guards.
        bool emptyModel = actor.ModelId is -1 or ushort.MaxValue && actor.NativeMovementBaseSpeed is not null && scene.NativeFieldInitialization?.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, hash) == true;
        return field.Id >= 72 && allocated && scene.NativeActorLayoutVersion == 1 && scene.NativeActorObjectIds.Contains(actor.ObjectId) && actor.NativeMotionAuthority is { Version: 1 } && (actor.ModelId >= 0 && actor.ModelId < field.Models.Count || emptyModel) && actor.PushRadius is >= 0 and <= ushort.MaxValue && actor.MovementSpeed is >= short.MinValue and <= short.MaxValue;
    }

}
