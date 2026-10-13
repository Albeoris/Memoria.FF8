using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Security.Cryptography;

namespace Memoria.FF8;

public static class NativeJoinMovementAdapter
{
    // TODO(TD-003): replace reached-field admission gates with the shared native JOIN contract
    public static bool Supports(FieldAssets field, SceneEventState scene, ActorState actor)
    {
        return IsFreshPartyBinding(field, scene, actor.ObjectId) && actor.ModelId >= 0 && actor.ModelId < field.Models.Count && actor.PushRadius is >= 0 and <= ushort.MaxValue || (IsReachedFollower(field, scene, actor.ObjectId) && actor.ModelId == 6 && actor.CharacterId == 5) || (IsReachedSoloLeader(field, scene, actor.ObjectId) && actor.ModelId == 0 && actor.CharacterId == 0) || NativeScriptMovementAdapter.SupportsCavernPartyBinding(field, scene, actor, true);
    }

    public static NativeJoinMovementResult? Resolve(FieldAssets field, SceneEventState scene, NativeJoinMovementQuery query)
    {
        bool cavern = field.Id == 129 && Convert.ToHexString(SHA256.HashData(field.Jsm)) == "CBF3122BECCE857482F93D49D96ECAC41B57B8A42776F9426779AC15EEA5D7E6" && query.ObjectId == 5 && scene.NativeFieldLoadVersion == 1 && scene.NativeActorLayoutVersion == 1 && scene.NativePartyTraceVersion == 1 && scene.NativeActorObjectIds.SequenceEqual(new[] { 0, 5, 9, 10, 11 }) && scene.Party.SequenceEqual(new[] { 0, 3 }) && scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 0, 1, 255 }) && query.Radius == 48 && query.Speed is 2696 or 7929 && query.HasActorCollision is not null;
        bool freshParty = IsFreshPartyBinding(field, scene, query.ObjectId) && query.Speed is 2696 or 7929 && query.HasActorCollision is not null;
        if ((!freshParty && !IsReachedFollower(field, scene, query.ObjectId) && !IsReachedSoloLeader(field, scene, query.ObjectId) && !cavern) || query.Direction is < 0 or > byte.MaxValue || query.Radius is < 0 or > ushort.MaxValue)
        {
            return null;
        }
        // Events checks every returned fan envelope before committing any actor state.
        FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, (byte)query.Direction, query.Speed, (ushort)query.Radius, false, true, 0, true);
        Func<FieldNativeGroundProbe, bool>? contacts = cavern || freshParty ? probe => query.HasActorCollision!(new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value)) : null;
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, scene.LockedTriangles, contacts);
        if (result.Status == FieldNativeMovementStatus.Unsupported || result.Proposals.Count is < 1 or > 16 || (cavern || freshParty) && result.Proposals.Any(proposal => proposal.FanProbes.Count != 3 || proposal.FanProbes.Any(probe => !probe.Supported || probe.NativeHeight is null)))
        {
            return null;
        }
        NativeJoinMovementProposal[] proposals = result.Proposals.Select(proposal => new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction)).ToArray();
        IReadOnlyList<NativePlayerGroundProbe>[]? probes = cavern || freshParty ? result.Proposals.Select(proposal => (IReadOnlyList<NativePlayerGroundProbe>)proposal.FanProbes.Select(probe => new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value, probe.ActorCollision)).ToArray()).ToArray() : null;
        return new NativeJoinMovementResult(result.Status == FieldNativeMovementStatus.Accepted, result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, proposals, probes);
    }

    private static bool IsFreshPartyBinding(FieldAssets field, SceneEventState scene, int objectId)
    {
        return scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } initialization && initialization.FieldId == field.Id && initialization.ProgramHash == Convert.ToHexString(SHA256.HashData(field.Jsm)) && scene.NativeFieldLoadVersion == 1 && scene.NativePartyTraceVersion == 1 && scene.NativeActorLayoutVersion == 1 && scene.FieldPartyNativeActorIds.Count == 3 && scene.FieldPartyNativeActorIds.Any(index => index != byte.MaxValue && index >= 0 && index < scene.NativeActorObjectIds.Count && scene.NativeActorObjectIds[index] == objectId);
    }

    private static bool IsReachedFollower(FieldAssets field, SceneEventState scene, int objectId)
    {
        return field.Id == 172 && scene.NativeActorLayoutVersion == 1 && scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 0, 1, 255 }) && scene.NativeActorObjectIds.Count > 1 && scene.NativeActorObjectIds[1] == objectId && objectId == 6;
    }

    private static bool IsReachedSoloLeader(FieldAssets field, SceneEventState scene, int objectId)
    {
        return field.Id == 172 && scene.NativeActorLayoutVersion == 1 && scene.Party.SequenceEqual(new[] { 0 }) && scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 0, 255, 255 }) && scene.NativeActorObjectIds.Count > 0 && scene.NativeActorObjectIds[0] == objectId && objectId == 0;
    }
}
