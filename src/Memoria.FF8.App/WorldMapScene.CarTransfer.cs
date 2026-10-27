using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState? PrepareCarReboard(WorldState state, NativeWorldSalaryState salary)
    {
        if (state.NativeFootRuntime?.ParkedCar is not { } parked || salary.InputHistory is not { PreviousButtons: { } previous } input || (input.CurrentButtons & ~previous & 0x40) == 0 || salary.EventUi?.ScriptMode != 0 || state.NativeFootRuntime.CameraTransition is { Flags: not 0 })
        {
            return null;
        }
        NativeFootWorldRuntimeState foot = state.NativeFootRuntime;
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(NativeWorldFootCameraState.Create(state));
        NativeWorldContactShape shape = assets.ReadNativeContactShape(parked.Car.ShapeId);
        NativeWorldPathWords player = context.RelativeWords(new(state.X, state.NativeHeight, -state.Z));
        NativeWorldPathWords anchor = (context.ProjectNode(parked.Position) ?? throw new NotSupportedException(context.UnsupportedReason)).Words;
        if (!WorldStationContactCertificate.EvaluatePolygon(shape, unchecked((short)(parked.BodyYaw - 1024)), player, anchor, 200, NativeWorldTerrain.ProbeRadius(foot.EntityType)))
        {
            return null;
        }
        foreach (NativeWorldGroundAlternative alternative in foot.GroundCache!.Alternatives)
        {
            WorldTriangle polygon = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements);
            uint flags = polygon.GroundType | ((uint)polygon.TextureFlags << 8) | ((uint)polygon.VertexFlags << 16);
            int height = state.NativeHeight - (polygon.GroundType <= 5 ? 320 : 0);
            if (Math.Abs(parked.Position.Y - height) >= 200 || !NativeWorldTerrain.CanDisembark(flags, foot.EntityType) || !NativeWorldTerrain.CanDisembark(flags, parked.Car.EntityType))
            {
                return null;
            }
        }
        IReadOnlyList<WorldNativeGardenProbe> probes = assets.CreateNativeGardenShapeFootprint(parked.Position, parked.Car.EntityType, parked.BodyYaw);
        if (CarTransferContact(state, parked.Car, probes, parked.Car.EntityType, 0, forceCircle: true) >= 0)
        {
            return null;
        }
        NativeCarWorldRuntimeState car = parked.Car with { AnimationVariant = 0, Speed = 0, Forest = foot.Forest, CameraTransition = new(183, foot.CameraTransition?.ProjectionDistance ?? 1024), PriorTerrainOcclusion = foot.PriorTerrainOcclusion, MapDisplayMode = foot.MapDisplayMode, GroundCache = foot.GroundCache with { Alternatives = foot.GroundCache.Alternatives.Select(item => item with { Entries = [] }).ToArray() }, GroundYaw = foot.GroundYaw, CameraFollowSpeed = session.State.WorldProcess!.CarCameraFollowSpeed, MovementMagnitude20409F4 = foot.MovementMagnitude20409F4, PrimaryContact = null, BroadContact = null };
        car = car with { ProjectionCamera = new NativeWorldFootCameraState(state.X, state.Z, state.NativeHeight, state.CameraTargetX, state.CameraTargetZ, state.CameraTargetNativeHeight, state.CameraYaw, foot.Forest!.CameraPitch, foot.CameraOffsetY, foot.CameraOffsetZ) { ProjectionDistance = foot.CameraTransition?.ProjectionDistance ?? 1024 } };
        return state with { X = parked.Position.X, Z = -parked.Position.Z, NativeHeight = parked.Position.Y, BodyYaw = parked.BodyYaw, NativeFootRuntime = null, NativeCarRuntime = car, AnimationId = 0, AnimationCounter = 0 };
    }

    private NativeTrainWorldAllocation CarTransferAllocation(NativeCarWorldRuntimeState car)
    {
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        return car.Allocation ?? assets.ReadTrainWorldAllocation(inputs.StoryWord, inputs.EventFlags265);
    }

    private int CarTransferContact(WorldState state, NativeCarWorldRuntimeState car, IReadOnlyList<WorldNativeGardenProbe> probes, int entity, int padding, NativeWorldParkedCar? parked = null, bool forceCircle = false)
    {
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(state.NativeFootRuntime is not null ? NativeWorldFootCameraState.Create(state) : car.ProjectionCamera ?? new(state.X, state.Z, state.NativeHeight, state.CameraTargetX, state.CameraTargetZ, state.CameraTargetNativeHeight, state.CameraYaw, -112, -341, -7168));
        WorldNativeGardenProbe center = probes[0];
        NativeWorldPathWords player = context.RelativeWords(new(center.X, center.Y, -center.Z));
        NativeWorldContactShape selected = assets.ReadNativeContactShape(WorldAssets.NativeGardenEntityShape(entity));
        int hit = WorldStationContactCertificate.Evaluate(assets, CarTransferAllocation(car), [], player, center.Y, position => context.ProjectNode(position) ?? throw new NotSupportedException(context.UnsupportedReason), context.IsNear, padding, NativeWorldTerrain.ProbeRadius(entity), selected.Height, forceCircle: forceCircle, selectedShape: selected).ContactIndex;
        if (hit >= 0 || parked is null || !context.IsNear(parked.Position))
        {
            return hit;
        }
        NativeWorldContactShape shape = assets.ReadNativeContactShape(car.ShapeId);
        NativeWorldPathWords anchor = (context.ProjectNode(parked.Position) ?? throw new NotSupportedException(context.UnsupportedReason)).Words;
        return player.Y - selected.Height <= anchor.Y + padding && player.Y >= anchor.Y - shape.Height - padding && WorldStationContactCertificate.EvaluatePolygon(shape, unchecked((short)(parked.BodyYaw - 1024)), player, anchor, padding, NativeWorldTerrain.ProbeRadius(entity)) ? CarTransferAllocation(car).Objects.Length : -1;
    }

    private WorldState? PrepareCarDisembark(WorldState state, NativeWorldSalaryState salary)
    {
        if (state.NativeCarRuntime is not { } car || salary.InputHistory is not { PreviousButtons: { } previous } input || (input.CurrentButtons & ~previous & 0x40) == 0 || salary.EventUi?.ScriptMode != 0 || state.Frame < 24 || car.CameraTransition is { Flags: not 0 })
        {
            return null;
        }
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        if ((inputs.EventFlags265 & 128) != 0)
        {
            return null;
        }
        NativeTrainWorldAllocation allocation = CarTransferAllocation(car);
        int footEntity = allocation.Objects[allocation.ControlledObjectIndex].EntityType;
        WorldGardenPlacementResult? foot = null;
        foreach (NativeWorldGroundAlternative alternative in car.GroundCache!.Alternatives)
        {
            WorldTriangle polygon = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements);
            uint flags = polygon.GroundType | ((uint)polygon.TextureFlags << 8) | ((uint)polygon.VertexFlags << 16);
            int height = state.NativeHeight - (polygon.GroundType <= 5 ? 320 : 0);
            NativeWorldPathPosition body = new(state.X, state.NativeHeight, -state.Z);
            int Contact(IReadOnlyList<WorldNativeGardenProbe> probes, int selected, int padding, bool circle) => CarTransferContact(state, car, probes, selected, padding, forceCircle: circle);
            WorldGardenPlacementResult vehicle = assets.ResolveNativeGardenPlacement(body, checked((short)state.BodyYaw), car.EntityType, car.EntityType, 0, height, flags, car.GroundCache, replacements, Contact);
            if (!vehicle.Accepted)
            {
                return null;
            }
            WorldGardenPlacementResult candidate = assets.ResolveNativeGardenPlacement(body, checked((short)state.BodyYaw), car.EntityType, footEntity, 256, height, flags, car.GroundCache, replacements, Contact);
            if (foot is not null && foot != candidate)
            {
                throw new NotSupportedException("Car disembark placement differs across retained ground alternatives.");
            }
            foot = candidate;
        }
        if (foot?.Accepted != true)
        {
            return null;
        }
        NativeWorldProcess process = session.State.WorldProcess!;
        NativeWorldForestState forest = (car.Forest ?? throw new NotSupportedException("Car disembark requires its retained loader forest owner; migrate this historical save explicitly.")) with { CameraCooldown = salary.CarCameraCooldown ?? process.CameraCooldown };
        NativeWorldParkedCar parked = new(car, new(state.X, state.NativeHeight, -state.Z), checked((short)state.BodyYaw), state.Frame);
        NativeFootWorldRuntimeState runtime = new()
        {
            ProofVersion = NativeFootWorldRuntimeState.CurrentProofVersion,
            Rule = NativeFootWorldRuntimeState.CarDismountRule,
            EntityType = checked((byte)footEntity),
            AnimationVariant = 0,
            MapDisplayMode = car.MapDisplayMode,
            CameraOffsetY = -341,
            CameraOffsetZ = -7168,
            CameraTransition = new(183, 1024),
            ReloadSourceWorldFrame = null,
            GroundCache = car.GroundCache with { Alternatives = car.GroundCache.Alternatives.Select(item => item with { Entries = [] }).ToArray() },
            GroundYaw = car.GroundYaw,
            PreviousHorizontalInput = 0,
            MovementMagnitude20409F4 = car.MovementMagnitude20409F4,
            Forest = forest,
            ParkedCar = parked,
            PriorTerrainOcclusion = false
        };
        NativeWorldPathPosition target = foot.Position!.Value;
        return state with { X = WrapCoordinate(target.X, 262144), Z = WrapCoordinate(-target.Z, 196608), NativeHeight = target.Y, NativeCarRuntime = null, NativeFootRuntime = runtime, AnimationId = 0, AnimationCounter = 0 };
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadParkedCarModels(WorldState state)
    {
        NativeWorldParkedCar parked = state.NativeFootRuntime!.ParkedCar!;
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(NativeWorldFootCameraState.Create(state));
        List<NativeWorldStaticModelDraw> result = [];
        NativeTrainWorldAllocation allocation = CarTransferAllocation(parked.Car);
        for (int index = 0; index < allocation.Objects.Length; index++)
        {
            NativeTrainWorldObject item = allocation.Objects[index];
            if (index == allocation.ControlledObjectIndex || item.ShapeId == 94 || !context.IsNear(item.Position))
            {
                continue;
            }
            NativeWorldPathNodeProjection projection = context.ProjectNode(item.Position) ?? throw new NotSupportedException(context.UnsupportedReason);
            if (projection.Visible)
            {
                result.Add(new(assets.ReadScriptedStaticModel(item.ShapeId), new(context.CanonicalModelAnchor(projection), WorldStaticModelTransform.AllocationAngles(item))) { ObjectId = new(WorldRenderObjectKind.Allocation, index) });
            }
        }
        NativeWorldPathNodeProjection carProjection = context.ProjectNode(parked.Position) ?? throw new NotSupportedException(context.UnsupportedReason);
        if (carProjection.Visible)
        {
            result.Add(new(assets.GetCarModel(parked.Car.EntityType), new(context.CanonicalModelAnchor(carProjection), new(checked((short)parked.Car.ModelTilt), unchecked((short)(parked.BodyYaw - 1024)), 0))) { ObjectId = new(WorldRenderObjectKind.Allocation, allocation.Objects.Length) });
        }
        return result;
    }
}
