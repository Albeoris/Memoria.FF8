using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Security.Cryptography;

namespace Memoria.FF8;

public static class NativeScriptMovementAdapter
{
    // TODO(TD-003): replace remaining field/object admission gates with shared native movement authority
    public static bool Supports(FieldAssets field, SceneEventState scene, ActorState actor)
    {
        if (actor.NativeMotionAuthority is { } authority && (authority.Version != 1 || authority.FacingHold > 1))
        {
            return false;
        }
        if (SupportsGenericBinding(field, scene, actor) || SupportsCavernPartyBinding(field, scene, actor))
        {
            return true;
        }
        if (!SupportsBinding(field, scene, actor.ObjectId) || actor.CharacterId != -2 || actor.PushRadius != 48)
        {
            return false;
        }
        return (field.Id, actor.ObjectId) switch
        {
            (165, 19) => actor.ModelId == 18 && actor.MovementDirectionLimit == 6 && actor.MovementSpeed == 6500,
            (165, 20) => actor.ModelId == 19 && actor.MovementDirectionLimit == 4 && actor.MovementSpeed == 2500,
            (168, 13 or 17) => actor.ModelId == 11 && actor.MovementDirectionLimit == 5 && actor.MovementSpeed == 2500 && !actor.CollisionEnabled && !actor.PushEnabled,
            (168, 16 or 18) => actor.ModelId == 14 && actor.MovementDirectionLimit == 5 && actor.MovementSpeed == 2500 && !actor.CollisionEnabled && !actor.PushEnabled,
            _ => false
        };
    }

    public static NativeScriptDirectionResult? ResolveDirection(FieldAssets field, SceneEventState scene, NativeScriptDirectionQuery query, ActorState? actor = null)
    {
        bool selected = (field.Id, query.ObjectId) switch
        {
            (165, 19) => query.Speed == 6500 && query.DirectionLimit == 6,
            (165, 20) => query.Speed == 2500 && query.DirectionLimit == 4,
            (168, 13 or 17) => query.Speed == 2500 && query.DirectionLimit == 5,
            (168, 16 or 18) => query.Speed == 2500 && query.DirectionLimit == 5,
            _ => false
        };
        bool generic = actor is not null && actor.ObjectId == query.ObjectId && (SupportsGenericBinding(field, scene, actor) || SupportsCavernPartyBinding(field, scene, actor)) && actor.MovementSpeed == query.Speed && (actor.MovementDirectionLimit == query.DirectionLimit || query.HoldsFacing && query.DirectionLimit == 0);
        if (!(SupportsBinding(field, scene, query.ObjectId) && selected || generic) || (generic ? query.Radius != actor!.PushRadius : query.Radius != 48) || (query.TargetX >> 12) is < short.MinValue or > short.MaxValue || (query.TargetY >> 12) is < short.MinValue or > short.MaxValue)
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
        bool selected = (field.Id, query.ObjectId) switch
        {
            (165, 19) => query.Speed == 6500,
            (165, 20) => query.Speed == 2500,
            (168, 13 or 17) => query.Speed == 2500,
            (168, 16 or 18) => query.Speed == 2500,
            _ => false
        };
        bool cavernParty = actor is not null && actor.ObjectId == query.ObjectId && SupportsCavernPartyBinding(field, scene, actor);
        bool generic = actor is not null && actor.ObjectId == query.ObjectId && (SupportsGenericBinding(field, scene, actor) || cavernParty) && actor.MovementSpeed == query.Speed;
        if (!(SupportsBinding(field, scene, query.ObjectId) && selected || generic) || (generic ? query.Radius != actor!.PushRadius : query.Radius != 48))
        {
            return null;
        }
        // Events verifies every returned native fan against retained actor peers
        // before publishing this pure geometry result, including blocked attempts.
        FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, query.Direction, query.Speed, query.Radius, false, true, 0, true);
        Func<FieldNativeGroundProbe, bool>? contacts = generic && query.HasActorCollision is not null ? probe => query.HasActorCollision(new NativeScriptGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value)) : null;
        if (generic && contacts is null)
        {
            return null;
        }
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, scene.LockedTriangles, contacts);
        if (result.Status == FieldNativeMovementStatus.Unsupported || result.Proposals.Count is < 1 or > 16 || result.Proposals.Any(proposal => proposal.FanProbes.Count != 3 || proposal.FanProbes.Any(probe => !probe.Supported || probe.NativeHeight is null)))
        {
            return null;
        }
        NativeScriptMovementProposal[] proposals = result.Proposals.Select(proposal => new NativeScriptMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction, proposal.FanProbes.Select(probe => new NativeScriptGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value, probe.ActorCollision)).ToArray())).ToArray();
        NativeScriptMovementStatus status = result.Status == FieldNativeMovementStatus.Accepted ? NativeScriptMovementStatus.Accepted : NativeScriptMovementStatus.Blocked;
        return new NativeScriptMovementResult(status, result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, proposals);
    }

    private static bool SupportsBinding(FieldAssets field, SceneEventState scene, int objectId)
    {
        if (scene.NativeActorLayoutVersion != 1 || !scene.NativeActorObjectIds.Contains(objectId))
        {
            return false;
        }
        return field.Id switch
        {
            165 => objectId is 19 or 20,
            168 => objectId is 13 or 16 or 17 or 18 && scene.NativeFieldLoadVersion == 1 && scene.NativeActorObjectIds.SequenceEqual(new[] { 0, 12, 13, 16, 17, 18, 19, 25, 26, 27 }),
            _ => false
        };
    }

    private static bool SupportsGenericBinding(FieldAssets field, SceneEventState scene, ActorState actor)
    {
        string hash = Convert.ToHexString(SHA256.HashData(field.Jsm));
        bool initialized = scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } initialization && initialization.FieldId == field.Id && initialization.ProgramHash == hash;
        // Native4789A0 drives every retained mode1 body, including536=FFFF.
        // Empty resource532890 and actor-contact47A720 have their own guards.
        bool emptyModel = actor.ModelId is -1 or ushort.MaxValue && actor.NativeMovementBaseSpeed is not null && scene.NativeFieldInitialization?.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, hash) == true;
        return field.Id >= 72 && (scene.NativeFieldLoadVersion == 1 || initialized) && scene.NativeActorLayoutVersion == 1 && scene.NativeActorObjectIds.Contains(actor.ObjectId) && actor.NativeMotionAuthority is { Version: 1, FacingHold: <= 1 } && (actor.ModelId >= 0 && actor.ModelId < field.Models.Count || emptyModel) && actor.PushRadius is >= 0 and <= ushort.MaxValue && actor.MovementSpeed is > 0 and <= short.MaxValue;
    }

    internal static bool SupportsCavernPartyBinding(FieldAssets field, SceneEventState scene, ActorState actor, bool joining = false)
    {
        return field.Id == 129 && Convert.ToHexString(SHA256.HashData(field.Jsm)) == "CBF3122BECCE857482F93D49D96ECAC41B57B8A42776F9426779AC15EEA5D7E6" && scene.NativeFieldLoadVersion == 1 && scene.NativeActorLayoutVersion == 1 && scene.NativePartyTraceVersion == 1 && scene.NativeActorObjectIds.SequenceEqual(new[] { 0, 5, 9, 10, 11 }) && scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 0, 1, 255 }) && scene.Party.SequenceEqual(new[] { 0, 3 }) && actor.ObjectId == 5 && actor.CharacterId == 3 && actor.ModelId == 5 && actor.PushRadius == 48 && actor.MovementDirectionLimit == 0 && (actor.MovementSpeed == 7929 || joining && actor.MovementSpeed == 2696) && actor.NativeMotionAuthority is { Version: 1, FacingHold: 0 };
    }
}
