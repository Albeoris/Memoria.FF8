using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeGardenWorldRuntimeState CreateCarGardenOwner(WorldTransition packet, NativeSavedWorldPose carPose, NativeWorldFootCameraState camera, NativeWorldGroundCacheState cache, NativeWorldProcess process, byte mapDisplay)
    {
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory ?? throw new NotSupportedException("Car Garden loading requires its actual world-entry owner.");
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Car Garden loading requires its actual saved GF13 flags.");
        NativeGardenWorldAllocation allocation = assets.ReadCarGardenAllocation(packet, entry, session.State.EventMemory, session.State.SavedWorld!, vitals.GuardianForces[13].Flags, carPose, checked((ushort)camera.Yaw), mapDisplay);
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        IReadOnlyDictionary<int, int> selected = WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, inputs.StoryByte));
        WorldGroundSelection ground = assets.LookupNativeGround(carPose.X, carPose.RawZ, selected, []) ?? throw new NotSupportedException("Fresh car Garden allocation has no actual initial ground.");
        NativeWorldAuxiliaryRelocationReceipt? auxiliary = assets.ReadGardenAuxiliaryRelocations(packet, session.State.EventMemory, carPose);
        return NativeGardenWorldRuntimeState.CreateFresh(session.State.SavedWorld!, allocation, cache, process, assets.ReadGardenCameraDefaults(), camera, unchecked((short)-(int)ground.Ground.Height)) with { AuxiliaryEntry = auxiliary, BackgroundRails = CreateBackgroundRails(allocation.Flags265, camera) };
    }

    private static bool HasCarActionEdge(WorldState state, NativeWorldSalaryState salary)
    {
        return salary.InputHistory is { PreviousButtons: { } previous } input && (input.CurrentButtons & ~previous & 0x40) != 0 && salary.EventUi?.ScriptMode == 0 && state.Frame >= 24;
    }

    private void ValidateCarGarden(WorldState state, NativeCarWorldRuntimeState car)
    {
        car.Validate();
        NativeGardenWorldRuntimeState garden = car.GardenObjects!;
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        assets.ValidateGardenAllocationResources(garden.Allocation);
        if (session.State.SavedWorld?.Slot4.ShapeId != car.ShapeId || garden.Allocation.Packet != session.State.WorldArrival || garden.Allocation.Story != inputs.StoryWord || (garden.Allocation.ReplacementFlags264 & 7) != (inputs.ReplacementFlags & 7))
        {
            throw new InvalidDataException("Car retained Garden allocation differs from its original packet, current story or selected saved vehicle.");
        }
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(car.ProjectionCamera!);
        if (garden.BackgroundRails is { } background)
        {
            ValidateBackgroundRails(state, background);
        }
        else if ((inputs.EventFlags265 & 1) == 0 && !WorldFootContactCertificate.ExcludesOriginalTrainRoutes(context))
        {
            throw new NotSupportedException("Car selected from Garden entered train proximity without represented preceding rail phases.");
        }
        assets.ValidateNativeGroundCache(car.GroundCache!, replacements);
    }

    private int CarGardenContact(WorldState state, NativeCarWorldRuntimeState car, IReadOnlyList<WorldNativeGardenProbe> probes, int selectedEntity, int padding, bool forceCircle = false)
    {
        NativeGardenWorldRuntimeState garden = car.GardenObjects!;
        NativeWorldFootCameraState camera = state.NativeFootRuntime is not null ? NativeWorldFootCameraState.Create(state) : car.ProjectionCamera!;
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        return WorldGardenContactCertificate.Evaluate(assets, garden.Allocation, garden.Objects, state.NativeFootRuntime?.EntityType ?? car.EntityType, selectedEntity, probes, projection, padding, forceCircle, (garden.Allocation.Flags265 & 1) != 0 || WorldFootContactCertificate.ExcludesOriginalTrainRoutes(projection), garden.BackgroundRails);
    }

    private WorldState? PrepareGardenCarReboard(WorldState state, NativeWorldSalaryState salary)
    {
        if (state.NativeGardenRuntime is not { } garden || state.NativeFootRuntime is not { } foot || garden.Allocation.SavedCarIndex < 0 || !HasCarActionEdge(state, salary) || foot.CameraTransition is { Flags: not 0 })
        {
            return null;
        }
        NativeGardenWorldObject body = garden.Objects[garden.Allocation.SavedCarIndex];
        byte entity = checked((byte)body.EntityType);
        if (body.ShapeId != NativeCarWorldRuntimeState.ShapeForEntity(entity))
        {
            throw new NotSupportedException("Garden car boarding requires its represented stored model variant.");
        }
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(state);
        NativeCarWorldRuntimeState car = NativeCarWorldRuntimeState.Create(session.State.SavedWorld!, foot.GroundCache!, session.State.WorldProcess!, entity, projectionCamera: camera, gardenObjects: garden);
        NativeWorldParkedCar parked = new(car, body.Position, checked((short)WrapYaw(body.RotationY)), 0) { LoadedFromSavedSlot = true };
        return PrepareGardenCarBoarding(state, salary, parked);
    }

    private WorldState? PrepareGardenCarBoarding(WorldState state, NativeWorldSalaryState salary, NativeWorldParkedCar parked)
    {
        NativeFootWorldRuntimeState foot = state.NativeFootRuntime!;
        NativeCarWorldRuntimeState car = parked.Car;
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(NativeWorldFootCameraState.Create(state));
        NativeWorldPathWords player = context.RelativeWords(new(state.X, state.NativeHeight, -state.Z));
        NativeWorldPathWords anchor = (context.ProjectNode(parked.Position) ?? throw new NotSupportedException(context.UnsupportedReason)).Words;
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
        if (CarGardenContact(state, car, probes, car.EntityType, 0, true) >= 0)
        {
            return null;
        }
        NativeWorldGroundCacheState cache = foot.GroundCache with { Alternatives = foot.GroundCache.Alternatives.Select(item => item with { Entries = [] }).ToArray() };
        NativeGardenWorldRuntimeState garden = car.GardenObjects! with { Forest = foot.Forest!, CameraPitch = foot.Forest!.CameraPitch, GroundCache = cache, GroundYaw = checked((short)foot.GroundYaw!.Value), ProjectionCamera = NativeWorldFootCameraState.Create(state) };
        garden = garden.PublishCarBody(parked.Position, parked.BodyYaw);
        car = car with { Boarding = NativeWorldCarBoardingReceipt.Create(state, session.State.WorldArrival!, parked), AnimationVariant = 0, Speed = 0, Forest = foot.Forest, CameraTransition = new(183, foot.CameraTransition?.ProjectionDistance ?? 1024), PriorTerrainOcclusion = foot.PriorTerrainOcclusion, MapDisplayMode = foot.MapDisplayMode, GroundCache = cache, GroundYaw = foot.GroundYaw, CameraFollowSpeed = session.State.WorldProcess!.CarCameraFollowSpeed, MovementMagnitude20409F4 = foot.MovementMagnitude20409F4, PrimaryContact = null, BroadContact = null, GardenObjects = garden, ProjectionCamera = NativeWorldFootCameraState.Create(state) };
        return state with { X = parked.Position.X, Z = -parked.Position.Z, NativeHeight = parked.Position.Y, BodyYaw = parked.BodyYaw, NativeFootRuntime = null, NativeGardenRuntime = null, NativeCarRuntime = car, AnimationId = 0, AnimationCounter = 0 };
    }

    private WorldState? PrepareCarGardenDisembark(WorldState state, NativeWorldSalaryState salary)
    {
        if (state.NativeCarRuntime is not { GardenObjects: not null } car || !HasCarActionEdge(state, salary) || car.CameraTransition is { Flags: not 0 } || (WorldEntryVariables.Read(session.State).EventFlags265 & 128) != 0)
        {
            return null;
        }
        WorldGardenPlacementResult? footPlacement = null;
        foreach (NativeWorldGroundAlternative alternative in car.GroundCache!.Alternatives)
        {
            WorldTriangle polygon = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements);
            uint flags = polygon.GroundType | ((uint)polygon.TextureFlags << 8) | ((uint)polygon.VertexFlags << 16);
            int height = state.NativeHeight - (polygon.GroundType <= 5 ? 320 : 0);
            NativeWorldPathPosition body = new(state.X, state.NativeHeight, -state.Z);
            int Contact(IReadOnlyList<WorldNativeGardenProbe> probes, int selected, int padding, bool circle) => CarGardenContact(state, car, probes, selected, padding, circle);
            WorldGardenPlacementResult vehicle = assets.ResolveNativeGardenPlacement(body, checked((short)state.BodyYaw), car.EntityType, car.EntityType, 0, height, flags, car.GroundCache, replacements, Contact);
            if (!vehicle.Accepted)
            {
                return null;
            }
            WorldGardenPlacementResult candidate = assets.ResolveNativeGardenPlacement(body, checked((short)state.BodyYaw), car.EntityType, car.FootEntityType, 256, height, flags, car.GroundCache, replacements, Contact);
            if (footPlacement is not null && footPlacement != candidate)
            {
                throw new NotSupportedException("Garden-allocation car exit differs across retained ground alternatives.");
            }
            footPlacement = candidate;
        }
        if (footPlacement?.Accepted != true)
        {
            return null;
        }
        NativeWorldForestState forest = car.Forest! with { CameraCooldown = salary.CarCameraCooldown ?? session.State.WorldProcess!.CameraCooldown };
        NativeWorldParkedCar parked = new(car, new(state.X, state.NativeHeight, -state.Z), checked((short)state.BodyYaw), state.Frame);
        NativeFootWorldRuntimeState foot = new() { ProofVersion = NativeFootWorldRuntimeState.CurrentProofVersion, Rule = NativeFootWorldRuntimeState.CarDismountRule, EntityType = car.FootEntityType, AnimationVariant = 0, MapDisplayMode = car.MapDisplayMode, CameraOffsetY = -341, CameraOffsetZ = -7168, CameraTransition = new(183, 1024), ReloadSourceWorldFrame = null, GroundCache = car.GroundCache with { Alternatives = car.GroundCache.Alternatives.Select(item => item with { Entries = [] }).ToArray() }, GroundYaw = car.GroundYaw, PreviousHorizontalInput = 0, MovementMagnitude20409F4 = car.MovementMagnitude20409F4, Forest = forest, ParkedCar = parked, PriorTerrainOcclusion = false };
        NativeWorldPathPosition target = footPlacement.Position!.Value;
        NativeGardenWorldRuntimeState garden = car.GardenObjects! with { Forest = forest, CameraPitch = forest.CameraPitch, GroundCache = foot.GroundCache, GroundYaw = checked((short)foot.GroundYaw!.Value) };
        garden = garden.PublishBody(target, 0, checked((short)state.BodyYaw), 0, false);
        foot = foot with { ParkedCar = parked with { Car = car with { GardenObjects = garden } } };
        return state with { X = WrapCoordinate(target.X, 262144), Z = WrapCoordinate(-target.Z, 196608), NativeHeight = target.Y, NativeCarRuntime = null, NativeFootRuntime = foot, AnimationId = 0, AnimationCounter = 0 };
    }

    private WorldState? PrepareCarGardenReboard(WorldState state, NativeWorldSalaryState salary)
    {
        if (state.NativeFootRuntime is not { ParkedCar.Car.GardenObjects: { } garden } foot || !HasCarActionEdge(state, salary) || foot.CameraTransition is { Flags: not 0 })
        {
            return null;
        }
        NativeGardenWorldObject target = garden.Objects[garden.Allocation.Garden64Index];
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(NativeWorldFootCameraState.Create(state));
        IReadOnlyList<WorldNativeGardenProbe> current = WorldAssets.CreateNativeGroundFootprint(state.X, state.Z, checked((short)state.NativeHeight), 32);
        bool excludeTrains = (garden.Allocation.Flags265 & 1) != 0 || WorldFootContactCertificate.ExcludesOriginalTrainRoutes(projection);
        int contact = WorldGardenContactCertificate.Evaluate(assets, garden.Allocation, garden.Objects, foot.EntityType, foot.EntityType, current, projection, 200, trainContactsExcluded: excludeTrains, rails: garden.BackgroundRails);
        if (contact != garden.Allocation.Garden64Index && contact != garden.Allocation.Garden65Index)
        {
            return null;
        }
        uint flags = GardenFootCurrentPolygonFlags(foot.GroundCache!);
        int groundHeight = state.NativeHeight - (state.GroundType <= 5 ? 320 : 0);
        if (!NativeGardenTransferState.PassesBoardingGate(foot.EntityType, 48, flags, groundHeight, target.Position.Y, true))
        {
            return null;
        }
        IReadOnlyList<WorldNativeGardenProbe> probes = assets.CreateNativeGardenShapeFootprint(target.Position, 48, target.RotationY);
        if (WorldGardenContactCertificate.Evaluate(assets, garden.Allocation, garden.Objects, foot.EntityType, 48, probes, projection, 0, true, excludeTrains) >= 0)
        {
            return null;
        }
        NativeWorldGroundCacheState cache = foot.GroundCache! with { Alternatives = foot.GroundCache.Alternatives.Select(item => item with { Entries = [] }).ToArray() };
        garden = garden with { Forest = foot.Forest!, CameraPitch = foot.Forest!.CameraPitch, GroundCache = cache, GroundYaw = checked((short)foot.GroundYaw!.Value), Transfer = null, TransferControls = garden.TransferControls with { TransitionPhase = int.MinValue }, MapDisplayMode = foot.MapDisplayMode, ProjectionCamera = NativeWorldFootCameraState.Create(state), CameraFollowSpeed = session.State.WorldProcess!.CarCameraFollowSpeed, ManualCameraVelocity = session.State.WorldProcess.VehicleManualCameraVelocity, PriorTerrainOcclusion = foot.PriorTerrainOcclusion };
        garden = garden with { Buttons = garden.Buttons.Observe(salary.InputHistory!.CurrentButtons) };
        WorldState boarded = CompleteGardenReboarding(state with { NativeGardenRuntime = garden, NativeFootRuntime = foot with { GroundCache = cache } }, target);
        return boarded;
    }

    private WorldState StepCarGardenObjects(WorldState state, out WorldFieldSelection? fieldEntry)
    {
        fieldEntry = null;
        NativeCarWorldRuntimeState? car = state.NativeCarRuntime ?? state.NativeFootRuntime?.ParkedCar?.Car;
        if (car?.GardenObjects is not { } garden)
        {
            return state;
        }
        NativeFootWorldRuntimeState? foot = state.NativeFootRuntime;
        NativeWorldForestState forest = foot?.Forest ?? car.Forest!;
        NativeWorldGroundCacheState cache = foot?.GroundCache ?? car.GroundCache!;
        NativeWorldFootCameraState camera = foot is null ? car.ProjectionCamera! : NativeWorldFootCameraState.Create(state);
        garden = garden with { Forest = forest!, CameraPitch = forest!.CameraPitch, GroundCache = cache!, GroundYaw = checked((short)(foot?.GroundYaw ?? car.GroundYaw)!.Value), ProjectionCamera = camera, AnimationVariant = foot?.AnimationVariant ?? car.AnimationVariant, MapDisplayMode = foot?.MapDisplayMode ?? car.MapDisplayMode, CurrentGroundHeight = unchecked((short)(state.NativeHeight - (state.GroundType <= 5 ? 320 : 0))), MovementMagnitude20409F4 = foot?.MovementMagnitude20409F4 ?? car.MovementMagnitude20409F4 ?? throw new InvalidDataException("Garden car allocation lost its actual accepted movement magnitude.") };
        garden = foot is null ? garden.PublishCarBody(new(state.X, state.NativeHeight, -state.Z), checked((short)state.BodyYaw)) : garden.PublishBody(new(state.X, state.NativeHeight, -state.Z), 0, checked((short)state.BodyYaw), 0, false);
        WorldState advanced = StepGardenCactuar(state with { NativeCarRuntime = null, NativeGardenRuntime = garden }, out fieldEntry);
        garden = advanced.NativeGardenRuntime!;
        car = car with { GardenObjects = garden, Forest = garden.Forest };
        return foot is null ? state with { NativeCarRuntime = car } : state with { NativeFootRuntime = foot with { Forest = garden.Forest, ParkedCar = foot.ParkedCar! with { Car = car } } };
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadCarGardenModels(WorldState state, NativeCarWorldRuntimeState car, bool includeCar)
    {
        NativeGardenWorldRuntimeState garden = car.GardenObjects!;
        NativeWorldFootCameraState camera = state.NativeFootRuntime is null ? car.ProjectionCamera! : NativeWorldFootCameraState.Create(state);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        List<NativeWorldStaticModelDraw> models = [];
        if (garden.BackgroundRails is { } background)
        {
            ValidateBackgroundRails(state, background, true);
            models.AddRange(ReadBackgroundRailModels(background));
        }
        for (int index = 0; index < garden.Objects.Length; index++)
        {
            NativeGardenWorldObject item = garden.Objects[index];
            if (index == garden.Allocation.FootObjectIndex || !includeCar && index == garden.Allocation.SavedCarIndex || !projection.IsNear(item.Position))
            {
                continue;
            }
            if (item.ShapeId == 94)
            {
                throw new NotSupportedException("Nearby Garden-allocation Cactuar presentation requires its executed pre-AI draw phase.");
            }
            if (item.ShapeId < 64)
            {
                throw new NotSupportedException("Nearby Garden allocation actor requires its actual animated world-model presentation.");
            }
            NativeWorldPathNodeProjection draw = projection.ProjectNode(item.Position) ?? throw new NotSupportedException(projection.UnsupportedReason);
            if (draw.Visible)
            {
                NativeWorldPathAngles angles = index == garden.Allocation.SavedCarIndex ? new(checked((short)car.ModelTilt), unchecked((short)(item.RotationY - 1024)), 0) : WorldStaticModelTransform.AnimatedAllocationAngles(item, car.AllocationAnimationCounter);
                models.Add(new(assets.ReadScriptedStaticModel(item.ShapeId), new(projection.CanonicalModelAnchor(draw), angles)) { ObjectId = new(WorldRenderObjectKind.Allocation, index) });
            }
        }
        return models;
    }
}
