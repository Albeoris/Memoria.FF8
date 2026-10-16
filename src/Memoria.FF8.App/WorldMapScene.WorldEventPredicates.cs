using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private bool IsNearWorldEventShape(WorldState accepted, WorldState cameraState, ushort shape)
    {
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
        NativeTrainWorldAllocation allocation = WorldEventStaticAllocation(accepted);
        RequireWorldEventShapeAllocation(accepted, shape);
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

    private bool? EvaluateWorldEventPredicate(WorldState state, byte scriptMode, NativeSavedWorldState saved, ushort opcode, ushort operand)
    {
        switch (opcode)
        {
            case 0xFF1B:
                if (state.NativeGardenRuntime is { } garden)
                {
                    if (!garden.Objects.Any(item => unchecked((sbyte)item.ShapeId) == operand))
                    {
                        return false;
                    }
                }
                else
                {
                    NativeTrainWorldAllocation allocation = WorldEventStaticAllocation(state);
                    RequireWorldEventShapeAllocation(state, operand);
                    if (!allocation.Objects.Any(item => unchecked((sbyte)item.ShapeId) == operand))
                    {
                        return false;
                    }
                }
                throw new NotSupportedException("World event FF1B requires its actual non-vehicle interaction contact owner for an allocated shape.");
            case 0xFF25:
                return scriptMode == operand;
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
                NativeWorldGroundCacheState cache = state.NativeFootRuntime?.GroundCache ?? state.NativeCarRuntime?.GroundCache ?? state.NativeTrainRuntime?.GroundCache ?? state.NativeGardenRuntime?.GroundCache ?? throw new NotSupportedException("World event FF32 requires the executed accepted-ground owner.");
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
