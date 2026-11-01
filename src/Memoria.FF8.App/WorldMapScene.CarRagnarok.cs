using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeRagnarokWorldRuntimeState CreateCarRagnarokOwner(WorldTransition packet, NativeSavedWorldPose carPose, NativeWorldFootCameraState camera, NativeWorldGroundCacheState cache, NativeWorldProcess process, byte mapDisplay)
    {
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeRagnarokWorldAllocation allocation = assets.ReadRagnarokCarAllocation(packet, session.State.WorldEntryMemory!, session.State.EventMemory, saved, session.State.CharacterVitals!, carPose, checked((ushort)camera.Yaw), mapDisplay);
        WorldGroundSelection selected = assets.LookupNativeGround(carPose.X, carPose.RawZ, replacements, [cache.Alternatives[0].CurrentGround]) ?? throw new NotSupportedException("Car Ragnarok allocation requires its actual initial terrain polygon.");
        short groundHeight = unchecked((short)-(int)selected.Ground.Height);
        NativeRagnarokWorldRuntimeState runtime = NativeRagnarokWorldRuntimeState.CreateAfterLoader(allocation, checked((short)process.CarCameraFollowSpeed), process.VerticalBias204DADC ?? NativeWorldVerticalBiasState.UnknownWord);
        NativeRagnarokTransferCamera common = assets.ReadRagnarokTransferCameraTargets(NativeRagnarokTransferKind.Landing, groundHeight, checked((short)carPose.Yaw));
        return runtime with { BackgroundRails = CreateBackgroundRails(WorldEntryVariables.Read(session.State).EventFlags265, camera), GroundCache = cache, GroundYaw = 0, ProjectionCamera = camera, CurrentGroundHeight = groundHeight, PrimaryContact = -1, BroadContact = -1, Forest = NativeWorldForestState.Create(process.MainLoopCounter, process.CameraCooldown) with { CameraPitch = camera.Pitch }, PreviousParticleNativeHeight = carPose.Height, ProjectionCurveThreshold = 5216, Cactuar = allocation.Objects.Any(item => item.ShapeId == 94) ? NativeCactuarWorldState.CreateAfterLoad() : null, TransferCamera = common with { ProjectionDistance = camera.ProjectionDistance, Pitch = camera.Pitch, Yaw = checked((short)camera.Yaw), OffsetZ = camera.OffsetZ }, ManualCameraVelocity = process.VehicleManualCameraVelocity };
    }

    private void ValidateCarRagnarok(WorldState state, NativeCarWorldRuntimeState car)
    {
        car.Validate();
        NativeRagnarokWorldRuntimeState runtime = car.RagnarokObjects!;
        runtime.RequireSceneState();
        assets.ValidateRagnarokAllocationResources(runtime.Allocation);
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        if (session.State.SavedWorld?.Slot4.ShapeId != car.ShapeId || runtime.Allocation.Packet != session.State.WorldArrival || runtime.Allocation.Story != inputs.StoryWord || (runtime.Allocation.ReplacementFlags264 & 7) != (inputs.ReplacementFlags & 7))
        {
            throw new InvalidDataException("Car retained Ragnarok allocation differs from its actual packet, current story or saved vehicle.");
        }
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(car.ProjectionCamera!);
        if (runtime.BackgroundRails is { } background)
        {
            ValidateBackgroundRails(state, background);
        }
        else if ((inputs.EventFlags265 & 1) == 0 && !WorldFootContactCertificate.ExcludesOriginalTrainRoutes(projection))
        {
            throw new NotSupportedException("Car selected from Ragnarok entered train proximity without represented preceding rail phases.");
        }
        if (runtime.BackgroundRails is { } rails)
        {
            ValidateBackgroundRails(state, rails);
        }
        assets.ValidateNativeGroundCache(car.GroundCache!, replacements);
    }

    private bool RagnarokCarTrainsExcluded(NativeWorldPathProjectionCertificate projection)
    {
        return (WorldEntryVariables.Read(session.State).EventFlags265 & 1) != 0 || WorldFootContactCertificate.ExcludesOriginalTrainRoutes(projection);
    }

    private int CarRagnarokContact(WorldState state, NativeCarWorldRuntimeState car, IReadOnlyList<WorldNativeGardenProbe> probes, int entity, int padding, bool forceCircle = false)
    {
        NativeWorldFootCameraState camera = state.NativeFootRuntime is null ? car.ProjectionCamera! : NativeWorldFootCameraState.Create(state);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        int currentEntity = state.NativeFootRuntime?.EntityType ?? car.EntityType;
        int ignored = NativeCarWorldRuntimeState.IsSupportedEntity(entity) || NativeCarWorldRuntimeState.IsSupportedEntity(currentEntity) ? car.RagnarokObjects!.Allocation.SavedCarIndex : -1;
        return WorldRagnarokContactCertificate.Evaluate(assets, car.RagnarokObjects!, probes, projection, padding, RagnarokCarTrainsExcluded(projection), entity, currentEntity, forceCircle, ignored);
    }

    private WorldState? PrepareRagnarokCarBoarding(WorldState state, NativeWorldSalaryState salary)
    {
        NativeFootWorldRuntimeState? foot = state.NativeFootRuntime;
        NativeRagnarokWorldRuntimeState? runtime = state.NativeRagnarokRuntime ?? foot?.ParkedCar?.Car.RagnarokObjects;
        if (runtime is null || foot is null || runtime.Allocation.SavedCarIndex < 0 || !HasCarActionEdge(state, salary) || foot.CameraTransition is { Flags: not 0 })
        {
            return null;
        }
        NativeRagnarokWorldObject body = runtime.Objects[runtime.Allocation.SavedCarIndex];
        byte entity = checked((byte)NativeTrainWorldObject.ClassifyShape(body.ShapeId));
        if (body.ShapeId != NativeCarWorldRuntimeState.ShapeForEntity(entity))
        {
            throw new NotSupportedException("Ragnarok car boarding requires its represented stored model variant.");
        }
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(state);
        NativeCarWorldRuntimeState car = NativeCarWorldRuntimeState.Create(session.State.SavedWorld!, foot.GroundCache!, session.State.WorldProcess!, entity, projectionCamera: camera, ragnarokObjects: runtime);
        NativeWorldParkedCar parked = new(car, body.Position, checked((short)WrapYaw(body.Yaw)), 0) { LoadedFromSavedSlot = true };
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        if (!projection.IsNear(parked.Position))
        {
            return null;
        }
        NativeWorldPathWords player = projection.RelativeWords(new(state.X, state.NativeHeight, -state.Z));
        NativeWorldPathWords anchor = (projection.ProjectNode(parked.Position) ?? throw new NotSupportedException(projection.UnsupportedReason)).Words;
        if (!WorldStationContactCertificate.EvaluatePolygon(assets.ReadNativeContactShape(car.ShapeId), unchecked((short)(parked.BodyYaw - 1024)), player, anchor, 200, NativeWorldTerrain.ProbeRadius(foot.EntityType)))
        {
            return null;
        }
        foreach (NativeWorldGroundAlternative alternative in foot.GroundCache!.Alternatives)
        {
            WorldTriangle polygon = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements);
            uint flags = polygon.GroundType | ((uint)polygon.TextureFlags << 8) | ((uint)polygon.VertexFlags << 16);
            int height = state.NativeHeight - (polygon.GroundType <= 5 ? 320 : 0);
            if (Math.Abs(parked.Position.Y - height) >= 200 || !NativeWorldTerrain.CanDisembark(flags, foot.EntityType) || !NativeWorldTerrain.CanDisembark(flags, car.EntityType))
            {
                return null;
            }
        }
        IReadOnlyList<WorldNativeGardenProbe> probes = assets.CreateNativeGardenShapeFootprint(parked.Position, car.EntityType, parked.BodyYaw);
        if (CarRagnarokContact(state, car, probes, car.EntityType, 0, true) >= 0)
        {
            return null;
        }
        NativeWorldGroundCacheState cache = foot.GroundCache with { Alternatives = foot.GroundCache.Alternatives.Select(item => item with { Entries = [] }).ToArray() };
        runtime = (runtime with { GroundCache = cache, GroundYaw = checked((short)foot.GroundYaw!.Value), Forest = foot.Forest, ProjectionCamera = camera, ProjectionCurveThreshold = 5216 }).PublishCarBody(parked.Position, parked.BodyYaw);
        car = car with { Boarding = NativeWorldCarBoardingReceipt.Create(state, session.State.WorldArrival!, parked), Forest = foot.Forest, GroundCache = cache, GroundYaw = foot.GroundYaw, CameraTransition = new(183, foot.CameraTransition?.ProjectionDistance ?? 1024), PriorTerrainOcclusion = foot.PriorTerrainOcclusion, MapDisplayMode = foot.MapDisplayMode, MovementMagnitude20409F4 = foot.MovementMagnitude20409F4, RagnarokObjects = runtime, ProjectionCamera = camera };
        return state with { X = parked.Position.X, Z = -parked.Position.Z, NativeHeight = parked.Position.Y, BodyYaw = parked.BodyYaw, NativeFootRuntime = null, NativeRagnarokRuntime = null, NativeCarRuntime = car, AnimationId = 0, AnimationCounter = 0 };
    }

    private WorldState? PrepareCarRagnarokDisembark(WorldState state, NativeWorldSalaryState salary)
    {
        if (state.NativeCarRuntime is not { RagnarokObjects: not null } car || !HasCarActionEdge(state, salary) || car.CameraTransition is { Flags: not 0 } || (WorldEntryVariables.Read(session.State).EventFlags265 & 128) != 0)
        {
            return null;
        }
        WorldGardenPlacementResult? placement = null;
        foreach (NativeWorldGroundAlternative alternative in car.GroundCache!.Alternatives)
        {
            WorldTriangle polygon = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements);
            uint flags = polygon.GroundType | ((uint)polygon.TextureFlags << 8) | ((uint)polygon.VertexFlags << 16);
            int height = state.NativeHeight - (polygon.GroundType <= 5 ? 320 : 0);
            NativeWorldPathPosition body = new(state.X, state.NativeHeight, -state.Z);
            int Contact(IReadOnlyList<WorldNativeGardenProbe> probes, int selected, int padding, bool circle) => CarRagnarokContact(state, car, probes, selected, padding, circle);
            if (!assets.ResolveNativeGardenPlacement(body, checked((short)state.BodyYaw), car.EntityType, car.EntityType, 0, height, flags, car.GroundCache, replacements, Contact).Accepted)
            {
                return null;
            }
            WorldGardenPlacementResult candidate = assets.ResolveNativeGardenPlacement(body, checked((short)state.BodyYaw), car.EntityType, car.FootEntityType, 256, height, flags, car.GroundCache, replacements, Contact);
            if (placement is not null && placement != candidate)
            {
                throw new NotSupportedException("Ragnarok-allocation car exit differs across retained ground alternatives.");
            }
            placement = candidate;
        }
        if (placement?.Accepted != true)
        {
            return null;
        }
        NativeWorldForestState forest = car.Forest! with { CameraCooldown = salary.CarCameraCooldown ?? session.State.WorldProcess!.CameraCooldown };
        NativeWorldParkedCar parked = new(car, new(state.X, state.NativeHeight, -state.Z), checked((short)state.BodyYaw), state.Frame);
        NativeWorldGroundCacheState cache = car.GroundCache! with { Alternatives = car.GroundCache.Alternatives.Select(item => item with { Entries = [] }).ToArray() };
        NativeRagnarokWorldRuntimeState runtime = (car.RagnarokObjects! with { GroundCache = cache, GroundYaw = checked((short)car.GroundYaw!.Value), Forest = forest }).PublishFootBody(placement.Position!.Value, 0, checked((short)state.BodyYaw), 0);
        NativeFootWorldRuntimeState foot = new() { ProofVersion = NativeFootWorldRuntimeState.CurrentProofVersion, Rule = NativeFootWorldRuntimeState.CarDismountRule, ReloadSourceWorldFrame = null, EntityType = car.FootEntityType, AnimationVariant = 0, MapDisplayMode = car.MapDisplayMode, CameraOffsetY = -341, CameraOffsetZ = -7168, CameraTransition = new(183, 1024), GroundCache = cache, GroundYaw = car.GroundYaw, PreviousHorizontalInput = 0, MovementMagnitude20409F4 = car.MovementMagnitude20409F4, Forest = forest, ParkedCar = parked with { Car = car with { RagnarokObjects = runtime } }, PriorTerrainOcclusion = false };
        NativeWorldPathPosition target = placement.Position.Value;
        return state with { X = WrapCoordinate(target.X, 262144), Z = WrapCoordinate(-target.Z, 196608), NativeHeight = target.Y, NativeCarRuntime = null, NativeRagnarokRuntime = null, NativeFootRuntime = foot, AnimationId = 0, AnimationCounter = 0 };
    }

    private WorldState? PrepareCarRagnarokReboard(WorldState state, NativeWorldSalaryState salary)
    {
        if (state.NativeFootRuntime is not { ParkedCar.Car.RagnarokObjects: { } runtime } foot || !HasCarActionEdge(state, salary) || foot.CameraTransition is { Flags: not 0 })
        {
            return null;
        }
        NativeRagnarokWorldObject ship = runtime.Objects[runtime.Allocation.RagnarokIndex];
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(state);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        IReadOnlyList<WorldNativeGardenProbe> current = WorldAssets.CreateNativeGroundFootprint(state.X, state.Z, checked((short)state.NativeHeight), 32);
        int hit = WorldRagnarokContactCertificate.Evaluate(assets, runtime, current, projection, 200, RagnarokCarTrainsExcluded(projection), foot.EntityType, foot.EntityType);
        if (hit != runtime.Allocation.RagnarokIndex)
        {
            return null;
        }
        uint flags = GardenFootCurrentPolygonFlags(foot.GroundCache!);
        int groundHeight = state.NativeHeight - (state.GroundType <= 5 ? 320 : 0);
        if (!NativeRagnarokTransferInput.PassesBoardingGate(foot.EntityType, flags, groundHeight, ship.Position.Y, true, true))
        {
            return null;
        }
        IReadOnlyList<WorldNativeGardenProbe> shape = assets.CreateNativeGardenShapeFootprint(ship.Position, 50, ship.Yaw);
        if (WorldRagnarokContactCertificate.Evaluate(assets, runtime, shape, projection, 0, RagnarokCarTrainsExcluded(projection), 50, foot.EntityType, true) >= 0)
        {
            return null;
        }
        NativeRagnarokTransferCamera globals = runtime.TransferCamera ?? throw new NotSupportedException("Ragnarok reboarding requires its retained common projection globals; migrate this historical snapshot explicitly.");
        runtime = runtime with { GroundCache = foot.GroundCache, GroundYaw = checked((short)foot.GroundYaw!.Value), Forest = foot.Forest, ProjectionCamera = camera, ProjectionCurveThreshold = 5216, CurrentGroundHeight = checked((short)groundHeight), TransferCamera = globals with { ProjectionDistance = camera.ProjectionDistance, CurveThreshold = 5216, Pitch = foot.Forest!.CameraPitch, Yaw = checked((short)state.CameraYaw), OffsetZ = foot.CameraOffsetZ }, Motor = runtime.Motor with { CameraPitch = foot.Forest!.CameraPitch } };
        ragnarokFootBoardingTarget = ship;
        return CompleteRagnarokFootReboarding(state with { NativeRagnarokRuntime = runtime, NativeCarRuntime = null });
    }

    private WorldState StepCarRagnarokObjects(WorldState state, out WorldFieldSelection? fieldEntry)
    {
        fieldEntry = null;
        NativeCarWorldRuntimeState? car = state.NativeCarRuntime ?? state.NativeFootRuntime?.ParkedCar?.Car;
        if (car?.RagnarokObjects is not { } runtime)
        {
            return state;
        }
        NativeFootWorldRuntimeState? foot = state.NativeFootRuntime;
        NativeWorldForestState forest = foot?.Forest ?? car.Forest!;
        NativeWorldGroundCacheState cache = foot?.GroundCache ?? car.GroundCache!;
        NativeWorldFootCameraState camera = foot is null ? car.ProjectionCamera! : NativeWorldFootCameraState.Create(state);
        short groundHeight = unchecked((short)(state.NativeHeight - (state.GroundType <= 5 ? 320 : 0)));
        runtime = runtime with { GroundCache = cache, GroundYaw = checked((short)(foot?.GroundYaw ?? car.GroundYaw)!.Value), Forest = forest, ProjectionCamera = camera, ProjectionCurveThreshold = 5216, CurrentGroundHeight = groundHeight, PriorTerrainOcclusion = foot?.PriorTerrainOcclusion ?? car.PriorTerrainOcclusion, Motor = runtime.Motor with { CameraPitch = forest.CameraPitch } };
        if (runtime.TransferCamera is { } globals)
        {
            runtime = runtime with { TransferCamera = globals with { ProjectionDistance = camera.ProjectionDistance, CurveThreshold = 5216, Pitch = camera.Pitch, Yaw = checked((short)camera.Yaw), OffsetZ = camera.OffsetZ } };
        }
        runtime = foot is null ? runtime.PublishCarBody(new(state.X, state.NativeHeight, -state.Z), checked((short)state.BodyYaw)) : runtime.PublishFootBody(new(state.X, state.NativeHeight, -state.Z), 0, checked((short)state.BodyYaw), 0);
        WorldState advanced = StepRagnarokCactuar(state with { NativeCarRuntime = null, NativeRagnarokRuntime = runtime }, out fieldEntry);
        runtime = advanced.NativeRagnarokRuntime!;
        car = car with { RagnarokObjects = runtime, Forest = runtime.Forest };
        return foot is null ? state with { NativeCarRuntime = car } : state with { NativeFootRuntime = foot with { Forest = runtime.Forest, ParkedCar = foot.ParkedCar! with { Car = car } } };
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadCarRagnarokModels(WorldState state, NativeCarWorldRuntimeState car, bool includeCar)
    {
        NativeRagnarokWorldRuntimeState runtime = car.RagnarokObjects!;
        NativeWorldFootCameraState camera = state.NativeFootRuntime is null ? car.ProjectionCamera! : NativeWorldFootCameraState.Create(state);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        List<NativeWorldStaticModelDraw> models = [];
        if (runtime.BackgroundRails is { } background)
        {
            ValidateBackgroundRails(state, background, true);
            models.AddRange(ReadBackgroundRailModels(background));
        }
        for (int index = 0; index < runtime.Objects.Length; index++)
        {
            NativeRagnarokWorldObject item = runtime.Objects[index];
            if (item.ShapeId is 0 or 1 or 3 || !includeCar && index == runtime.Allocation.SavedCarIndex || !projection.IsNear(item.Position))
            {
                continue;
            }
            if (item.ShapeId < 64)
            {
                throw new NotSupportedException("Nearby Ragnarok-allocation auxiliary actor requires its original animated presentation.");
            }
            if (item.ShapeId == 94)
            {
                throw new NotSupportedException("Nearby Ragnarok-allocation Cactuar presentation requires its executed pre-AI draw phase.");
            }
            NativeWorldPathNodeProjection draw = projection.ProjectNode(item.Position) ?? throw new NotSupportedException(projection.UnsupportedReason);
            if (draw.Visible)
            {
                NativeWorldPathAngles angles = index == runtime.Allocation.SavedCarIndex ? new(checked((short)car.ModelTilt), unchecked((short)(item.Yaw - 1024)), 0) : WorldStaticModelTransform.AnimatedAllocationAngles(item, car.AllocationAnimationCounter);
                models.Add(new(assets.ReadScriptedStaticModel(item.ShapeId), new(projection.CanonicalModelAnchor(draw), angles)) { ObjectId = new(WorldRenderObjectKind.Allocation, index) });
            }
        }
        return models;
    }

    private IReadOnlyList<NativeWorldAnimatedModelDraw> ReadCarRagnarokAnimatedModels(WorldState state, NativeCarWorldRuntimeState car)
    {
        NativeRagnarokWorldRuntimeState runtime = car.RagnarokObjects!;
        NativeRagnarokWorldObject ship = runtime.Objects[runtime.Allocation.RagnarokIndex];
        NativeWorldFootCameraState camera = state.NativeFootRuntime is null ? car.ProjectionCamera! : NativeWorldFootCameraState.Create(state);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        if (!projection.IsNear(ship.Position))
        {
            return [];
        }
        NativeWorldPathNodeProjection draw = projection.ProjectNode(ship.Position) ?? throw new NotSupportedException(projection.UnsupportedReason);
        return draw.Visible ? [new(assets.GetRagnarokModel(), projection.CanonicalModelAnchor(draw), ship.Yaw * MathF.Tau / 4096, 0, 0) { ObjectId = new(WorldRenderObjectKind.Allocation, runtime.Allocation.RagnarokIndex) }] : [];
    }
}
