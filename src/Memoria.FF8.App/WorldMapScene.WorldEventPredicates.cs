using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private bool IsNearWorldEventShape(WorldState accepted, WorldState cameraState, ushort shape)
    {
        if (accepted.NativeRagnarokRuntime is not null)
        {
            return IsNearRagnarokWorldEventShape(accepted, cameraState, shape);
        }
        WorldFieldSelectionInput input = new(accepted.X, accepted.Z, 0, 0);
        if (accepted.NativeGardenRuntime is { } garden)
        {
            return WorldAllocatedShapeProximity.Evaluate(input, shape, garden.Objects, index =>
            {
                NativeGardenWorldRuntimeState source = cameraState.NativeGardenRuntime ?? throw new NotSupportedException("World event FF17 lost its actual pre-terrain Garden object publication.");
                NativeWorldPathPosition position = source.Objects[index].Position;
                NativeWorldPathProjectionCertificate projection = GardenProjection(accepted);
                return projection.IsNear(position) && (projection.ProjectNode(position) ?? throw new NotSupportedException(projection.UnsupportedReason)).Visible;
            });
        }
        RequireWorldEventShapeAllocation(accepted, shape);
        if (IsAbsentFromOriginalFootAllocation(accepted, shape))
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
                if (state.NativeRagnarokRuntime is { } ragnarok)
                {
                    int FindRagnarokShape(byte shape) => ragnarok.Objects.Select((item, index) => (item, index)).Where(pair => pair.item.ShapeId == shape).Select(pair => pair.index).DefaultIfEmpty(-1).Single();
                    int[] excluded = [FindRagnarokShape(0), FindRagnarokShape(2), FindRagnarokShape(3), ragnarok.Allocation.RagnarokIndex, -1, FindRagnarokShape(64), FindRagnarokShape(65)];
                    return WorldAllocatedContactPredicate.Evaluate(new(state.X, state.Z, 0, 0), opcode, operand, state.BodyYaw, ragnarok.PrimaryContact, ragnarok.BroadContact, ragnarok.Objects.Length, index => ragnarok.Objects[index].ShapeId, index => ragnarok.Objects[index].Position, excluded);
                }
                if (state.NativeGardenRuntime is { } garden)
                {
                    int FindShape(byte shape) => garden.Objects.Select((item, index) => (item, index)).Where(pair => pair.item.ShapeId == shape).Select(pair => pair.index).DefaultIfEmpty(-1).Single();
                    int car = garden.Objects.Select((item, index) => (item, index)).Where(pair => pair.item.EntityType is >= 32 and <= 40 or 132).Select(pair => pair.index).DefaultIfEmpty(-1).Single();
                    int[] excluded = [garden.Allocation.FootObjectIndex, FindShape(2), FindShape(3), FindShape(1), car, garden.Allocation.Garden64Index, garden.Allocation.Garden65Index];
                    return WorldAllocatedContactPredicate.Evaluate(new(state.X, state.Z, 0, 0), opcode, operand, state.BodyYaw, garden.PrimaryContact, garden.BroadContact, garden.Objects.Length, index => garden.Objects[index].ShapeId, index => garden.Objects[index].Position, excluded);
                }
                else
                {
                    RequireWorldEventShapeAllocation(state, operand);
                    if (IsAbsentFromOriginalFootAllocation(state, operand))
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
                //544860 copies event BYTE271 to saved-world BYTE109. The normal gameplay callback has no2040A34 preview override.
                NativeScalarValue flag109 = session.State.EventMemory.Read(271, 1);
                if (!flag109.IsExact)
                {
                    throw new NotSupportedException("World predicate FF21 requires the exact executed event BYTE271 value.");
                }
                return WorldFieldEntryReader.MatchesSavedWorldFlag109(checked((byte)flag109.Values[0]), operand);
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
            case 0xFF32:
                NativeWorldGroundCacheState cache = state.NativeFootRuntime?.GroundCache ?? state.NativeCarRuntime?.GroundCache ?? state.NativeTrainRuntime?.GroundCache ?? state.NativeGardenRuntime?.GroundCache ?? state.NativeRagnarokRuntime?.GroundCache ?? throw new NotSupportedException("World event FF32 requires the executed accepted-ground owner.");
                bool? passed = null;
                foreach (NativeWorldGroundAlternative alternative in cache.Alternatives)
                {
                    byte flags = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).TextureFlags;
                    bool candidate = operand != ((flags & 8) == 0 ? 1 : 0);
                    if (passed is bool prior && prior != candidate)
                    {
                        throw new NotSupportedException("World event FF32 differs across retained accepted ground alternatives.");
                    }
                    passed = candidate;
                }
                return passed ?? throw new InvalidDataException("World event FF32 has no accepted ground alternative.");
            default:
                throw new NotSupportedException($"Reached unsupported world event predicate 0x{opcode:X4}/{operand}.");
        }
    }

    private static NativeTrainWorldAllocation WorldEventStaticAllocation(WorldState state)
    {
        return state.NativeFootRuntime?.NormalObjects?.Allocation ?? state.NativeFootRuntime?.StationTrains?.Load.Allocation ?? state.NativeCarRuntime?.Allocation ?? state.NativeTrainRuntime?.Load.Allocation ?? throw new NotSupportedException("World shape predicates require the executed dense allocation; this runtime retains only a partial loader receipt.");
    }

    private bool IsAbsentFromOriginalFootAllocation(WorldState state, ushort shape)
    {
        if (state.NativeFootRuntime is not { EntityType: 0 or 1, NormalObjects: null, StationTrains: null } || state.NativeGardenRuntime is not null || state.NativeRagnarokRuntime is not null)
        {
            return false;
        }
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        NativeWorldAllocationClause clause = assets.ReadWorldAllocationClause(inputs.StoryWord);
        NativeSavedWorldState saved = session.State.SavedWorld!;
        //544860 selects section9 and can append the saved car; the separate94/79/80 producers are checked by the caller.
        //Native shape comparisons sign-extend the allocated byte before comparing the predicate's WORD operand.
        bool declared = shape <= sbyte.MaxValue && clause.ContainsShape((byte)shape);
        bool savedCar = shape <= sbyte.MaxValue && saved.Slot4.ShapeId == shape && NativeTrainWorldObject.ClassifyShape(saved.Slot4.ShapeId) is >= 32 and <= 40 or 132;
        return !declared && !savedCar;
    }

    private void RequireWorldEventShapeAllocation(WorldState state, ushort shape)
    {
        //544860 appends94 independently of section9, and appends the current saved car.
        //These records are absent from the section9-only allocation receipt.
        if (shape == 94 || shape is 79 or 80 && (WorldEntryVariables.Read(session.State).ReplacementFlags & 7) != 0 || state.NativeCarRuntime is { } car && shape == car.ShapeId)
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
        throw new NotSupportedException("World event FF17 has no executed native projection owner.");
    }
}
