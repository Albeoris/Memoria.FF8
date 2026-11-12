using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private static NativeGardenWorldRuntimeState? WorldEventGardenObjects(WorldState state)
    {
        return state.NativeGardenRuntime ?? state.NativeCarRuntime?.GardenObjects ?? state.NativeFootRuntime?.ParkedCar?.Car.GardenObjects ?? state.NativeChocoboRuntime?.Objects;
    }

    private static NativeRagnarokWorldRuntimeState? WorldEventRagnarokObjects(WorldState state)
    {
        return state.NativeRagnarokRuntime ?? state.NativeCarRuntime?.RagnarokObjects ?? state.NativeFootRuntime?.ParkedCar?.Car.RagnarokObjects;
    }

    private static NativeCarWorldRuntimeState? WorldEventCarOwner(WorldState state)
    {
        return state.NativeCarRuntime ?? state.NativeFootRuntime?.ParkedCar?.Car;
    }

    private IReadOnlyList<NativeTrainWorldObject> WorldEventCarAllocationObjects(WorldState state, NativeCarWorldRuntimeState car)
    {
        return car.Allocation!.Objects.AddRange(CarContactAppendedObjects(state, car));
    }

    private bool IsNearWorldEventShape(WorldState accepted, WorldState cameraState, ushort shape)
    {
        if (WorldEventRagnarokObjects(accepted) is { } ragnarok)
        {
            NativeRagnarokWorldRuntimeState source = WorldEventRagnarokObjects(cameraState) ?? throw new NotSupportedException("World event FF17 lost its actual pre-terrain Ragnarok object publication.");
            NativeWorldPathProjectionCertificate projection = WorldEventProjection(cameraState);
            for (int index = 1; index < ragnarok.Objects.Length; index++)
            {
                NativeWorldPathPosition position = source.Objects[index].Position;
                if (shape != unchecked((sbyte)ragnarok.Objects[index].ShapeId) || !projection.IsNear(position) || !(projection.ProjectNode(position) ?? throw new NotSupportedException(projection.UnsupportedReason)).Visible)
                {
                    continue;
                }
                short dx = unchecked((short)(ragnarok.Objects[index].Position.X - accepted.X));
                short dz = unchecked((short)(ragnarok.Objects[index].Position.Z + accepted.Z));
                if (unchecked(dx * dx + dz * dz) <= 9000000)
                {
                    return true;
                }
            }
            return false;
        }
        if (WorldEventCarOwner(accepted) is { Allocation: not null } car)
        {
            IReadOnlyList<NativeTrainWorldObject> objects = WorldEventCarAllocationObjects(accepted, car);
            NativeCarWorldRuntimeState source = WorldEventCarOwner(cameraState) ?? throw new NotSupportedException("World event FF17 lost its actual pre-terrain car allocation.");
            IReadOnlyList<NativeTrainWorldObject> drawObjects = WorldEventCarAllocationObjects(cameraState, source);
            NativeWorldPathProjectionCertificate? projection = null;
            for (int index = 1; index < objects.Count; index++)
            {
                NativeTrainWorldObject item = objects[index];
                if (shape != unchecked((sbyte)item.ShapeId))
                {
                    continue;
                }
                if (item.EntityType is >= 0 and <= 9 or 128 or 49)
                {
                    throw new NotSupportedException("World event FF17 requires its allocated foot object's executed drawing eligibility.");
                }
                projection ??= WorldEventProjection(cameraState);
                NativeWorldPathPosition position = drawObjects[index].Position;
                if (!projection.IsNear(position) || !(projection.ProjectNode(position) ?? throw new NotSupportedException(projection.UnsupportedReason)).Visible)
                {
                    continue;
                }
                short dx = unchecked((short)(item.Position.X - accepted.X));
                short dz = unchecked((short)(item.Position.Z + accepted.Z));
                if (unchecked(dx * dx + dz * dz) <= 9000000)
                {
                    return true;
                }
            }
            return false;
        }
        WorldFieldSelectionInput input = new(accepted.X, accepted.Z, 0, 0);
        if (WorldEventGardenObjects(accepted) is { } garden)
        {
            return WorldAllocatedShapeProximity.Evaluate(input, shape, garden.Objects, index =>
            {
                NativeGardenWorldRuntimeState source = WorldEventGardenObjects(cameraState) ?? throw new NotSupportedException("World event FF17 lost its actual pre-terrain Garden object publication.");
                NativeWorldPathPosition position = source.Objects[index].Position;
                NativeWorldFootCameraState camera = accepted.NativeCarRuntime?.ProjectionCamera ?? (accepted.NativeFootRuntime is not null ? NativeWorldFootCameraState.Create(accepted) : garden.ProjectionCamera) ?? throw new NotSupportedException("World event FF17 lost its current Garden-allocation projection.");
                NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
                return projection.IsNear(position) && (projection.ProjectNode(position) ?? throw new NotSupportedException(projection.UnsupportedReason)).Visible;
            });
        }
        RequireWorldEventShapeAllocation(accepted, shape);
        if (IsAbsentFromOriginalAllocation(accepted, shape))
        {
            return false;
        }
        NativeTrainWorldAllocation allocation = WorldEventStaticAllocation(accepted);
        NativeWorldPathProjectionCertificate? context = null;
        for (int index = 1; index < allocation.Objects.Length; index++)
        {
            NativeTrainWorldObject item = allocation.Objects[index];
            if (shape != unchecked((sbyte)item.ShapeId))
            {
                continue;
            }
            if (item.EntityType is >= 0 and <= 9 or 128 or 49)
            {
                throw new NotSupportedException("World event FF17 requires the allocated foot object's executed5472C0 eligibility and body publication.");
            }
            context ??= WorldEventProjection(accepted);
            if (!context.IsNear(item.Position) || !(context.ProjectNode(item.Position) ?? throw new NotSupportedException(context.UnsupportedReason)).Visible)
            {
                continue;
            }
            short x = unchecked((short)(item.Position.X - input.X));
            short z = unchecked((short)(item.Position.Z + input.RawZ));
            if (unchecked(x * x + z * z) <= 9000000)
            {
                return true;
            }
        }
        return false;
    }

    internal bool? EvaluateWorldEventPredicate(WorldState state, byte scriptMode, NativeSavedWorldState saved, ushort opcode, ushort operand, WorldFieldInputContext? callbackInput = null, NativeWorldEventUi? eventUi = null)
    {
        switch (opcode)
        {
            case 0xFF18:
                return operand == 50 ? scriptMode == 6 : operand == 48 && scriptMode == 9;
            case 0xFF19:
                return operand == 50 ? scriptMode == 5 : operand == 48 && scriptMode == 8;
            case 0xFF1A:
            case 0xFF1B:
            case 0xFF1C:
            case 0xFF1D:
                if (WorldEventRagnarokObjects(state) is { } ragnarok)
                {
                    int FindRagnarokShape(byte shape) => ragnarok.Objects.Select((item, index) => (item, index)).Where(pair => pair.item.ShapeId == shape).Select(pair => pair.index).DefaultIfEmpty(-1).Single();
                    int[] excluded = [FindRagnarokShape(0), FindRagnarokShape(2), FindRagnarokShape(3), ragnarok.Allocation.RagnarokIndex, ragnarok.Allocation.SavedCarIndex, FindRagnarokShape(64), FindRagnarokShape(65)];
                    return WorldAllocatedContactPredicate.Evaluate(new(state.X, state.Z, 0, 0), opcode, operand, state.BodyYaw, state.NativeCarRuntime is { } selectedRagnarokCar ? selectedRagnarokCar.PrimaryContact : ragnarok.PrimaryContact, state.NativeCarRuntime is { } selectedRagnarokBroadCar ? selectedRagnarokBroadCar.BroadContact : ragnarok.BroadContact, ragnarok.Objects.Length, index => ragnarok.Objects[index].ShapeId, index => ragnarok.Objects[index].Position, excluded);
                }
                if (WorldEventCarOwner(state) is { Allocation: { } carAllocation } carRuntime)
                {
                    IReadOnlyList<NativeTrainWorldObject> objects = WorldEventCarAllocationObjects(state, carRuntime);
                    int? primary = state.NativeFootRuntime is { } foot ? foot.PrimaryContact : carRuntime.PrimaryContact;
                    int? broad = state.NativeFootRuntime is { } broadFoot ? broadFoot.BroadContact : carRuntime.BroadContact;
                    bool hasRailTail = carRuntime.ObjectProcess is { Primary: not null } or { Alternate: not null };
                    return WorldAllocatedContactPredicate.Evaluate(new(state.X, state.Z, 0, 0), opcode, operand, state.BodyYaw, primary, broad, objects.Count, index => objects[index].ShapeId, index => objects[index].Position, [carAllocation.ControlledObjectIndex, CarSelectedAllocationIndex(carRuntime)], hasRailTail);
                }
                if (WorldEventGardenObjects(state) is { } garden)
                {
                    int FindShape(byte shape) => garden.Objects.Select((item, index) => (item, index)).Where(pair => pair.item.ShapeId == shape).Select(pair => pair.index).DefaultIfEmpty(-1).Single();
                    int car = garden.Objects.Select((item, index) => (item, index)).Where(pair => pair.item.EntityType is >= 32 and <= 40 or 132).Select(pair => pair.index).DefaultIfEmpty(-1).Single();
                    int[] excluded = [garden.Allocation.FootObjectIndex, FindShape(2), FindShape(3), FindShape(1), car, garden.Allocation.Garden64Index, garden.Allocation.Garden65Index];
                    return WorldAllocatedContactPredicate.Evaluate(new(state.X, state.Z, 0, 0), opcode, operand, state.BodyYaw, state.NativeCarRuntime?.PrimaryContact ?? garden.PrimaryContact, state.NativeCarRuntime?.BroadContact ?? garden.BroadContact, garden.Objects.Length, index => garden.Objects[index].ShapeId, index => garden.Objects[index].Position, excluded);
                }
                else
                {
                    RequireWorldEventShapeAllocation(state, operand);
                    if (IsAbsentFromOriginalAllocation(state, operand))
                    {
                        return false;
                    }
                    NativeTrainWorldAllocation allocation = WorldEventStaticAllocation(state);
                    if (!allocation.Objects.Any(item => unchecked((sbyte)item.ShapeId) == operand))
                    {
                        return false;
                    }
                }
                throw new NotSupportedException($"World event0x{opcode:X4} requires its actual allocated primary/broad contact owner.");
            case 0xFF20:
                return eventUi?.CloseSuppressedThisCallback == true ? false : callbackInput?.Matches(operand);
            case 0xFF21:
                return saved.MatchesTerrainFlags109(operand);
            case 0xFF25:
                return (state.NativeRagnarokRuntime?.Transfer is not null ? scriptMode : eventUi?.ScriptMode ?? scriptMode) == operand;
            case 0xFF2C:
                return eventUi?.MatchesTextComplete(unchecked((sbyte)operand), unchecked((byte)(operand >> 8)));
            case 0xFF29:
                if (CurrentDialogue is null)
                {
                    return false;
                }
                throw new NotSupportedException("World event FF29 requires its actual active window result owner.");
            case 0xFF2D:
            case 0xFF30:
            case 0xFF31:
                sbyte selector = unchecked((sbyte)operand);
                if (selector is not (0 or 1) || (selector == 0 ? saved.WorldEventVariable124 : saved.WorldEventVariable125) is null)
                {
                    return null;
                }
                return saved.MatchesWorldEventVariable(opcode, operand);
            case 0xFF38:
                int? acceptedMagnitude = state.NativeFootRuntime is { } movementFoot ? movementFoot.MovementMagnitude20409F4 : state.NativeCarRuntime is { } movementCar ? movementCar.MovementMagnitude20409F4 : WorldEventGardenObjects(state)?.MovementMagnitude20409F4;
                return acceptedMagnitude is int magnitude ? operand == (magnitude != 0 ? 1 : 0) : null;
            case 0xFF32:
            case 0xFF33:
                NativeWorldGroundCacheState cache = state.NativeFootRuntime?.GroundCache ?? state.NativeCarRuntime?.GroundCache ?? state.NativeTrainRuntime?.GroundCache ?? state.NativeGardenRuntime?.GroundCache ?? state.NativeRagnarokRuntime?.GroundCache ?? state.NativeChocoboRuntime?.GroundCache ?? throw new NotSupportedException($"World event {opcode:X4} requires the executed accepted-ground owner.");
                bool? passed = null;
                foreach (NativeWorldGroundAlternative alternative in cache.Alternatives)
                {
                    WorldTriangle ground = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements);
                    //546100 compares FF32 as a WORD and FF33 as its low unsigned BYTE.
                    bool candidate = opcode == 0xFF32 ? operand != ((ground.TextureFlags & 8) == 0 ? 1 : 0) : unchecked((byte)operand) == ground.GroundType;
                    if (passed is bool prior && prior != candidate)
                    {
                        throw new NotSupportedException($"World event {opcode:X4} differs across retained accepted ground alternatives.");
                    }
                    passed = candidate;
                }
                return passed ?? throw new InvalidDataException($"World event {opcode:X4} has no accepted ground alternative.");
            default:
                throw new NotSupportedException($"Reached unsupported world event predicate 0x{opcode:X4}/{operand}.");
        }
    }

    private static NativeTrainWorldAllocation WorldEventStaticAllocation(WorldState state)
    {
        return state.NativeFootRuntime?.NormalObjects?.Allocation ?? state.NativeFootRuntime?.StationTrains?.Load.Allocation ?? state.NativeFootRuntime?.ParkedCar?.Car.Allocation ?? state.NativeCarRuntime?.Allocation ?? state.NativeTrainRuntime?.Load.Allocation ?? throw new NotSupportedException("World shape predicates require the executed dense allocation; this runtime retains only a partial loader receipt.");
    }

    private bool IsAbsentFromOriginalAllocation(WorldState state, ushort shape)
    {
        if (WorldEventGardenObjects(state) is not null || WorldEventRagnarokObjects(state) is not null || WorldEventCarOwner(state)?.Allocation is not null)
        {
            return false;
        }
        bool sparseFoot = state.NativeFootRuntime is { EntityType: 0 or 1, NormalObjects: null, StationTrains: null };
        bool sparseCar = state.NativeCarRuntime is { EntityType: 33, Allocation: null } && state.NativeFootRuntime is null;
        bool sparseScripted = state.NativeScriptedRuntime is not null && state.NativeFootRuntime is null && state.NativeCarRuntime is null && state.NativeTrainRuntime is null;
        if ((!sparseFoot && !sparseCar && !sparseScripted) || state.NativeGardenRuntime is not null || state.NativeRagnarokRuntime is not null)
        {
            return false;
        }
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        if (sparseScripted)
        {
            NativeScriptedWorldLoadReceipt load = state.NativeScriptedRuntime!.Load;
            load.Validate();
            if (load.FootRecord0 != assets.ReadScriptedFootRecord(inputs.StoryWord, inputs.EventFlags265))
            {
                throw new InvalidDataException("Scripted shape absence requires its authenticated complete selected allocation.");
            }
        }
        NativeWorldAllocationClause clause = assets.ReadWorldAllocationClause(inputs.StoryWord);
        NativeSavedWorldState saved = session.State.SavedWorld!;
        //544860 selects section9 and can append the saved car; the separate94/79/80 producers are checked by the caller.
        //Native shape comparisons sign-extend the allocated byte before comparing the predicate's WORD operand.
        bool declared = shape <= sbyte.MaxValue && clause.ContainsShape((byte)shape);
        bool savedCar = shape <= sbyte.MaxValue && saved.Slot4.ShapeId == shape && NativeTrainWorldObject.ClassifyShape(saved.Slot4.ShapeId) is >= 32 and <= 40 or 132;
        if (sparseScripted && shape <= sbyte.MaxValue && state.NativeScriptedRuntime!.Load.Slot4.ShapeId == shape && NativeTrainWorldObject.ClassifyShape((byte)shape) is >= 32 and <= 40 or 132)
        {
            savedCar = true;
        }
        return !declared && !savedCar;
    }

    private void RequireWorldEventShapeAllocation(WorldState state, ushort shape)
    {
        if (WorldEventGardenObjects(state) is not null || WorldEventRagnarokObjects(state) is not null || WorldEventCarOwner(state)?.Allocation is not null)
        {
            return;
        }
        //544860 appends94 independently of section9, and appends the current saved car.
        //These records are absent from the section9-only allocation receipt.
        bool scriptedShape = state.NativeScriptedRuntime is { } scripted && shape <= sbyte.MaxValue && NativeTrainWorldObject.ClassifyShape((byte)shape) == scripted.Path.EntityType;
        if (shape == 94 || shape is 79 or 80 && (WorldEntryVariables.Read(session.State).ReplacementFlags & 7) != 0 || state.NativeCarRuntime is { } car && shape == car.ShapeId || scriptedShape)
        {
            throw new NotSupportedException($"World shape predicate {shape} requires its actual appended544860 object publication.");
        }
    }

    private static NativeWorldPathProjectionCertificate WorldEventProjection(WorldState state)
    {
        if (state.NativeFootRuntime?.NormalObjects is { } normal)
        {
            return NativeWorldPathProjectionCertificate.CreateFoot(normal.Camera);
        }
        if (state.NativeFootRuntime?.StationTrains is { } station)
        {
            return state.NativeFootRuntime.StationTrainProcess?.ProjectionCamera is { } camera ? NativeWorldPathProjectionCertificate.CreateFoot(camera) : NativeWorldPathProjectionCertificate.Create(station.Camera, station.BodyPosition, station.EntityType);
        }
        if (state.NativeCarRuntime is not null)
        {
            return CarProjection(state);
        }
        if (state.NativeTrainRuntime is { } train)
        {
            return NativeWorldPathProjectionCertificate.Create(train.Camera, train.BodyPosition, train.EntityType);
        }
        if (state.NativeRagnarokRuntime is not null)
        {
            return RagnarokProjection(state);
        }
        if (state.NativeFootRuntime is not null)
        {
            return NativeWorldPathProjectionCertificate.CreateFoot(NativeWorldFootCameraState.Create(state));
        }
        if (WorldEventGardenObjects(state) is { } garden)
        {
            return NativeWorldPathProjectionCertificate.CreateFoot(garden.ProjectionCamera ?? throw new NotSupportedException("World event projection requires its current Garden camera."));
        }
        throw new NotSupportedException("World event FF17 has no executed native projection owner.");
    }
}
