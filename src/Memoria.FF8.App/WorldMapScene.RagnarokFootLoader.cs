using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private void InitializeParkedRagnarokFoot(WorldTransition arrival, WorldEntryVariables inputs, NativeSavedWorldFootReturn? battleReload = null)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory ?? throw new NotSupportedException("Parked Ragnarok reload requires its actual field/world entry bytes.");
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Parked Ragnarok reload requires actual GF13 allocation flags.");
        NativeWorldAuxiliaryRelocationReceipt? retainedAuxiliary = session.State.World?.NativeRagnarokRuntime?.Allocation.AuxiliaryEntry;
        NativeRagnarokWorldAllocation allocation = assets.ReadParkedRagnarokAllocation(arrival, entry, session.State.EventMemory, saved, vitals, battleReload, retainedAuxiliary);
        if (session.State.World is null)
        {
            NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Parked Ragnarok reload lost its native process owner.");
            NativeSavedWorldPose pose = allocation.LoadPose;
            WorldGroundSelection ground = assets.LookupNativeGround(pose.X, pose.RawZ, replacements, []) ?? throw new NotSupportedException("Parked Ragnarok foot reload has no actual original current ground polygon.");
            NativeWorldGroundCacheState cache = CreateFreshGroundCache(ground.Identity);
            NativeWorldForestState forest = NativeWorldForestState.Create(process.MainLoopCounter, process.CameraCooldown);
            NativeFootWorldRuntimeState foot = (battleReload is not null ? NativeFootWorldRuntimeState.CreateBattleReload(battleReload) : unchecked((sbyte)arrival.EntryIndex) == 62 ? NativeFootWorldRuntimeState.CreateSavedReload(saved.FootReturn!) : NativeFootWorldRuntimeState.CreateFirstNormalLoad(saved)) with { GroundCache = cache, Forest = forest };
            forest = forest with { CameraPitch = foot.AnimationVariant == 0 ? (short)-112 : (short)-512 };
            foot = foot with { Forest = forest };
            NativeRagnarokWorldRuntimeState runtime = NativeRagnarokWorldRuntimeState.CreateAfterLoader(allocation, checked((short)process.CarCameraFollowSpeed), process.VerticalBias204DADC ?? NativeWorldVerticalBiasState.UnknownWord);
            NativeWorldFootCameraState camera = new(pose.X, pose.RawZ, pose.Height, pose.X, pose.RawZ, pose.Height - 256, allocation.CameraYaw, forest.CameraPitch, -341, -7168) { ProjectionDistance = foot.CameraTransition?.ProjectionDistance ?? 1024 };
            short groundHeight = unchecked((short)-(int)ground.Ground.Height);
            WorldState? previous = session.State.DepartingWorldState;
            // The ordinary branch of late 554860 selects the ground tables for non-Ragnarok entities.
            NativeRagnarokTransferCamera globals = previous?.NativeRagnarokRuntime?.TransferCamera
                ?? (previous is null ? assets.ReadColdWorldProjection(camera) : assets.ReadRagnarokTransferCameraTargets(NativeRagnarokTransferKind.Landing, groundHeight));
            runtime = runtime with { ManualCameraVelocity = process.VehicleManualCameraVelocity, GroundCache = cache, GroundYaw = 0, Forest = forest, Cactuar = allocation.Objects.Any(item => item.ShapeId == 94) ? NativeCactuarWorldState.CreateAfterLoad() : null, ProjectionCamera = camera, ProjectionCurveThreshold = 5216, CurrentGroundHeight = groundHeight, PrimaryContact = -1, BroadContact = -1, PreviousButtons = null, PriorTerrainOcclusion = ReadInitialTerrainOcclusion(), TransferCamera = globals with { ProjectionDistance = camera.ProjectionDistance, CurveThreshold = 5216, Pitch = camera.Pitch, Yaw = checked((short)camera.Yaw), OffsetZ = camera.OffsetZ }, Motor = runtime.Motor with { CameraPitch = forest.CameraPitch } };
            runtime = runtime with { BackgroundRails = CreateBackgroundRails(inputs.EventFlags265, runtime.ProjectionCamera!) };
            foot.RequireNativeGroundState();
            runtime.RequireSceneState();
            session.SetWorldState(new WorldState { X = pose.X, Z = pose.RawZ, NativeHeight = pose.Height, BodyYaw = pose.Yaw, CameraYaw = allocation.CameraYaw, CameraTargetX = pose.X, CameraTargetZ = pose.RawZ, CameraTargetNativeHeight = pose.Height - 256, GroundType = ground.Ground.Triangle.GroundType, ReplacementMask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, inputs.StoryByte), NativeFootRuntime = foot, NativeRagnarokRuntime = runtime, EventUi = NativeWorldEventUi.Create() });
            if (allocation.AuxiliaryEntry is { } auxiliary)
            {
                session.SetEventMemory(auxiliary.ApplyTo(session.State.EventMemory));
            }
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
