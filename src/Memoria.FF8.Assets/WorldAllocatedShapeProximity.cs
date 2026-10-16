using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>546100/FF17 ordered allocated-shape predicate, after5472C0's5470E0 eligibility publication.</summary>
public static class WorldAllocatedShapeProximity
{
    public static bool Evaluate(WorldFieldSelectionInput input, ushort shape, IReadOnlyList<NativeGardenWorldObject> objects, Func<int, bool> eligible)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(eligible);
        //546100 starts its shared shape search after index0, not before the dense allocation.
        for (int index = 1; index < objects.Count; index++)
        {
            NativeGardenWorldObject item = objects[index];
            if (shape != unchecked((sbyte)item.ShapeId) || !eligible(index))
            {
                continue;
            }
            short x = unchecked((short)(item.Position.X - input.X));
            short z = unchecked((short)(item.Position.Z + input.RawZ));
            //45E0B0/45F930/45E450 narrow the relative vector to WORDs and sum only X/Z squares.
            if (unchecked(x * x + z * z) <= 9000000)
            {
                return true;
            }
        }
        return false;
    }
}
