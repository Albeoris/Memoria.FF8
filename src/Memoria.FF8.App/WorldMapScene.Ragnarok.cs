using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private void InitializeRagnarok(WorldTransition arrival, WorldEntryVariables inputs)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory ?? throw new NotSupportedException("Ragnarok allocation requires current executed entry bytes.");
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Ragnarok allocation requires current original GF13 flags.");
        NativeRagnarokWorldAllocation allocation = assets.ReadRagnarokAllocation(arrival, entry, session.State.EventMemory, saved, vitals, saved.RagnarokPose2, saved.RagnarokReturn?.CameraYaw96 ?? saved.GardenReturn?.CameraYaw96 ?? saved.FootReturn?.CameraYaw96, saved.RagnarokReturn is not null ? (byte)0 : saved.GardenReturn?.AnimationVariant108 ?? saved.FootReturn?.AnimationVariant108, session.State.World?.NativeRagnarokRuntime?.Allocation.AuxiliaryEntry);
        if (session.State.World is null)
        {
            NativeSavedWorldPose pose = allocation.LoadPose;
            WorldGroundSelection ground = assets.LookupNativeGround(pose.X, pose.RawZ, replacements, []) ?? throw new NotSupportedException("Ragnarok loader has no actual original initial ground polygon.");
            NativeWorldGroundCacheState cache = CreateFreshGroundCache(ground.Identity);
            NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Ragnarok loader requires its retained native process owner.");
            process.Validate();
            NativeWorldVerticalBiasState bias = process.VerticalBias204DADC ?? NativeWorldVerticalBiasState.UnknownWord;
            NativeRagnarokWorldRuntimeState runtime = NativeRagnarokWorldRuntimeState.CreateAfterLoader(allocation, checked((short)process.CarCameraFollowSpeed), bias);
            NativeRagnarokCameraDefaults defaults = assets.ReadRagnarokCameraDefaults();
            runtime = runtime with { GroundCache = cache, CurrentGroundHeight = unchecked((short)-(int)ground.Ground.Height), PrimaryContact = -1, BroadContact = -1, ProjectionCamera = new(pose.X, pose.RawZ, pose.Height, pose.X, pose.RawZ, pose.Height - 256, allocation.CameraYaw, runtime.Motor.CameraPitch, defaults.OffsetY, defaults.OffsetZ) };
            runtime = runtime with { GroundYaw = 0, ProjectionCurveThreshold = 6144, Forest = NativeWorldForestState.Create(process.MainLoopCounter, process.CameraCooldown) with { CameraPitch = runtime.Motor.CameraPitch }, Cactuar = allocation.Objects.Any(item => item.ShapeId == 94) ? NativeCactuarWorldState.CreateAfterLoad() : null };
            runtime.RequireSceneState();
            session.SetWorldState(new WorldState
            {
                X = pose.X,
                Z = pose.RawZ,
                NativeHeight = pose.Height,
                BodyYaw = pose.Yaw,
                CameraYaw = allocation.CameraYaw,
                CameraTargetX = pose.X,
                CameraTargetZ = pose.RawZ,
                CameraTargetNativeHeight = pose.Height - 256,
                GroundType = ground.Ground.Triangle.GroundType,
                ReplacementMask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, inputs.StoryByte),
                NativeRagnarokRuntime = runtime,
                EventUi = NativeWorldEventUi.Create()
            });
            if (allocation.AuxiliaryEntry is { } auxiliaryEntry)
            {
                session.SetEventMemory(auxiliaryEntry.ApplyTo(session.State.EventMemory));
            }
        }
        NativeRagnarokWorldRuntimeState restored = State.NativeRagnarokRuntime ?? throw new InvalidDataException("Ragnarok restore requires its retained scene runtime.");
        restored.RequireSceneState();
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(allocation), JsonSerializer.SerializeToElement(restored.Allocation)))
        {
            throw new InvalidDataException("Restored Ragnarok allocation differs from actual original loader inputs.");
        }
        assets.ValidateNativeGroundCache(restored.GroundCache!, replacements);
        _ = assets.GetRagnarokModel();
    }

    private static NativeWorldPathProjectionCertificate RagnarokProjection(WorldState state)
    {
        NativeWorldFootCameraState camera = state.NativeRagnarokRuntime?.ProjectionCamera ?? throw new NotSupportedException("Ragnarok requires its actual pre-terrain projection camera.");
        return NativeWorldPathProjectionCertificate.CreateRagnarok(camera, state.NativeRagnarokRuntime!.ProjectionCurveThreshold ?? (state.NativeFootRuntime is null ? 6144 : state.NativeRagnarokRuntime.TransferCamera?.CurveThreshold ?? 5216));
    }

    private int RagnarokContact(WorldState state, IReadOnlyList<WorldNativeGardenProbe> probes, int padding)
    {
        NativeWorldPathProjectionCertificate projection = RagnarokProjection(state);
        return WorldRagnarokContactCertificate.Evaluate(assets, state.NativeRagnarokRuntime!, probes, projection, padding, WorldFootContactCertificate.ExcludesOriginalTrainRoutes(projection));
    }

    private bool IsNearRagnarokWorldEventShape(WorldState accepted, WorldState cameraState, ushort shape)
    {
        NativeRagnarokWorldRuntimeState source = cameraState.NativeRagnarokRuntime!;
        NativeWorldPathProjectionCertificate projection = RagnarokProjection(accepted);
        for (int index = 1; index < source.Objects.Length; index++)
        {
            NativeRagnarokWorldObject item = source.Objects[index];
            if (shape != unchecked((sbyte)item.ShapeId) || !projection.IsNear(item.Position) || !(projection.ProjectNode(item.Position) ?? throw new NotSupportedException(projection.UnsupportedReason)).Visible)
            {
                continue;
            }
            short dx = unchecked((short)(accepted.NativeRagnarokRuntime!.Objects[index].Position.X - accepted.X));
            short dz = unchecked((short)(accepted.NativeRagnarokRuntime.Objects[index].Position.Z + accepted.Z));
            if (unchecked(dx * dx + dz * dz) <= 9000000)
            {
                return true;
            }
        }
        return false;
    }

    private WorldRenderFrame PrepareRagnarokFrame(WorldState state)
    {
        NativeFootWorldRuntimeState? foot = state.NativeFootRuntime;
        NativeRagnarokWorldRuntimeState runtime = state.NativeRagnarokRuntime!;
        runtime.RequireSceneState();
        NativeWorldPathProjectionCertificate projection = RagnarokProjection(state);
        var models = new List<NativeWorldStaticModelDraw>();
        List<NativeWorldAnimatedModelDraw> animated = [];
        for (int index = 0; index < runtime.Objects.Length; index++)
        {
            NativeRagnarokWorldObject item = runtime.Objects[index];
            if (item.ShapeId is 0 or 3 || item.ShapeId == 1 && foot is null || !projection.IsNear(item.Position))
            {
                continue;
            }
            if (item.ShapeId == 1)
            {
                NativeWorldPathNodeProjection ship = projection.ProjectNode(item.Position) ?? throw new NotSupportedException(projection.UnsupportedReason);
                if (ship.Visible)
                {
                    animated.Add(new(assets.GetRagnarokModel(), projection.CanonicalModelAnchor(ship), item.Yaw * MathF.Tau / 4096, 0, 0) { ObjectId = new(WorldRenderObjectKind.Allocation, index) });
                }
                continue;
            }
            if (item.ShapeId < 64 || item.ShapeId == 94)
            {
                throw new NotSupportedException("Nearby Ragnarok auxiliary actor requires its executed original animation/process publication.");
            }
            NativeWorldPathNodeProjection node = projection.ProjectNode(item.Position) ?? throw new NotSupportedException(projection.UnsupportedReason);
            if (node.Visible)
            {
                NativeWorldPathAngles angles = WorldStaticModelTransform.AllocationAngles(item.ShapeId, item.Pitch, item.Yaw, item.Roll);
                models.Add(new(assets.ReadScriptedStaticModel(item.ShapeId), new(projection.CanonicalModelAnchor(node), angles)) { ObjectId = new(WorldRenderObjectKind.Allocation, index) });
            }
        }
        NativeWorldFootCameraState words = runtime.ProjectionCamera!;
        WorldCamera camera = foot is not null ? FootCamera(state) : WorldCamera.Foot(state.CameraYaw, 2048) with { TargetPosition = new(state.CameraTargetX, -state.CameraTargetNativeHeight, state.CameraTargetZ), NativePitch = runtime.Motor.CameraPitch, NativeOffsetY = words.OffsetY, NativeOffsetZ = words.OffsetZ };
        WorldSky sky = WorldTerrainSelection.SelectSky(assets.Skies, state.X, state.Z, storyByte);
        bool avatarVisible = foot is null || state.GroundType is null or > 5;
        return new WorldRenderFrame(new Vector3(state.X, -state.NativeHeight, state.Z), state.BodyYaw * MathF.Tau / 4096, foot is null ? 0 : state.AnimationId, foot is null ? 0 : state.AnimationCounter >> 4, camera, sky, avatarVisible, AvatarModel: foot is null ? assets.GetRagnarokModel() : assets.GetFootModel(foot.EntityType), StaticModels: models, Forest: foot?.Forest, AnimatedModels: animated);
    }
}
