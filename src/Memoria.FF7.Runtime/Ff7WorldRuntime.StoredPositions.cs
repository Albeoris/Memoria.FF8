using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldStoredPosition(int X = 0, int Z = 0);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldStoredPosition[]? storedPositions;

    public int ReadStoredPositionDistance(int activeModel, int slot)
    {
        if ((uint)slot >= 3)
        {
            return 0;
        }
        Ff7WorldEntity entity = RequireEntity(activeModel);
        Ff7WorldStoredPosition point = storedPositions?[slot] ?? new();
        return (Math.Abs(WrappedDifference(entity.X - point.X, 0x48000)) + Math.Abs(WrappedDifference(entity.Z - point.Z, 0x38000))) >> 4;
    }

    private bool ExecuteStoredPosition(WorldHostRequest request)
    {
        WorldOperation operation = request.Command.Operation;
        if (operation is not (WorldOperation.SavePosition or WorldOperation.FaceSavedPosition or WorldOperation.CopyModelPosition))
        {
            return false;
        }
        Ff7WorldEntity entity = RequireEntity(request.ActiveModel);
        int value = request.Command.Arguments[0];
        if (operation == WorldOperation.CopyModelPosition)
        {
            if (Machine.IsModelAttached(value) && entities.TryGetValue(value, out Ff7WorldEntity? source))
            {
                UpdateEntity(entity with { X = source.X, Y = source.Y, Z = source.Z, PreviousX = source.PreviousX, PreviousY = source.PreviousY, PreviousZ = source.PreviousZ, Flags = entity.Flags | 1, PresentationEpoch = entity.PresentationEpoch + 1 });
            }
        }
        else if ((uint)value < 3)
        {
            storedPositions ??= [new(), new(), new()];
            if (operation == WorldOperation.SavePosition)
            {
                storedPositions[value] = new(entity.X, entity.Z);
            }
            else
            {
                Ff7WorldStoredPosition point = storedPositions[value];
                int heading = inputMath.Direction(point.X - entity.X, point.Z - entity.Z);
                UpdateEntity(entity with { Direction = heading, TargetDirection = heading, ScriptDirection = null });
            }
        }
        return true;
    }
}
