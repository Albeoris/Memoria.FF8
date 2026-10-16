using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private void InitializeParkedRagnarokFoot(WorldTransition arrival, WorldEntryVariables inputs)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory ?? throw new NotSupportedException("Parked Ragnarok reload requires its actual field/world entry bytes.");
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Parked Ragnarok reload requires actual GF13 allocation flags.");
        NativeRagnarokWorldAllocation allocation = assets.ReadParkedRagnarokAllocation(arrival, entry, session.State.EventMemory, saved, vitals);
        if (session.State.World is null)
        {
            NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Parked Ragnarok reload lost its native process owner.");
            NativeRagnarokWorldRuntimeState previous = session.State.DepartingWorldState?.NativeRagnarokRuntime ?? throw new NotSupportedException("Parked Ragnarok reload requires its retained prior world camera/projection owner.");
            previous.RequireSceneState();
            NativeRagnarokTransferCamera globals = previous.TransferCamera ?? throw new NotSupportedException("Parked Ragnarok reload lost its native live projection words.");
            NativeSavedWorldPose pose = allocation.LoadPose;
            WorldGroundSelection ground = assets.LookupNativeGround(pose.X, pose.RawZ, replacements, []) ?? throw new NotSupportedException("Parked Ragnarok foot reload has no actual original current ground polygon.");
            NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity, previous.GroundCache);
            NativeWorldForestState forest = NativeWorldForestState.Create(process.MainLoopCounter, process.CameraCooldown);
            NativeFootWorldRuntimeState foot = NativeFootWorldRuntimeState.CreateSavedReload(saved.FootReturn!) with { GroundCache = cache, Forest = forest };
            NativeRagnarokWorldRuntimeState runtime = NativeRagnarokWorldRuntimeState.CreateAfterLoader(allocation, checked((short)process.CarCameraFollowSpeed), process.VerticalBias204DADC ?? NativeWorldVerticalBiasState.UnknownWord);
            NativeWorldFootCameraState camera = new(pose.X, pose.RawZ, pose.Height, pose.X, pose.RawZ, pose.Height - 256, allocation.CameraYaw, -112, -341, -7168);
            runtime = runtime with { GroundCache = cache, GroundYaw = 0, Forest = forest, Cactuar = allocation.Objects.Any(item => item.ShapeId == 94) ? NativeCactuarWorldState.CreateAfterLoad() : null, ProjectionCamera = camera, ProjectionCurveThreshold = 5216, CurrentGroundHeight = unchecked((short)-(int)ground.Ground.Height), PrimaryContact = -1, BroadContact = -1, PreviousButtons = null, PriorTerrainOcclusion = previous.PriorTerrainOcclusion, TransferCamera = globals with { ProjectionDistance = 1024, CurveThreshold = 5216, Pitch = -112, Yaw = checked((short)allocation.CameraYaw), OffsetZ = -7168 }, Motor = runtime.Motor with { CameraPitch = -112 } };
            foot.RequireNativeGroundState();
            runtime.RequireSceneState();
            session.SetWorldState(new WorldState { X = pose.X, Z = pose.RawZ, NativeHeight = pose.Height, BodyYaw = pose.Yaw, CameraYaw = allocation.CameraYaw, CameraTargetX = pose.X, CameraTargetZ = pose.RawZ, CameraTargetNativeHeight = pose.Height - 256, GroundType = ground.Ground.Triangle.GroundType, ReplacementMask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, inputs.StoryByte), NativeFootRuntime = foot, NativeRagnarokRuntime = runtime });
        }
        NativeRagnarokWorldRuntimeState restored = State.NativeRagnarokRuntime ?? throw new InvalidDataException("Parked Ragnarok restore lost its actual allocation.");
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(allocation), JsonSerializer.SerializeToElement(restored.Allocation)))
        {
            throw new InvalidDataException("Parked Ragnarok restored allocation differs from its original loader inputs.");
        }
        State.NativeFootRuntime?.RequireNativeGroundState();
        assets.ValidateNativeGroundCache(restored.GroundCache!, replacements);
    }
}
