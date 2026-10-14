using System.Numerics;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private int[] NativePolygonColorBodies()
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeFieldInitialization is null || program is null)
        {
            throw new NotSupportedException("Native polygon color requires its executed normal612-byte actor allocation.");
        }
        return Scene.NativeActorLayoutVersion == 1 ? Scene.NativeActorObjectIds.ToArray() : program.NativeObjects.Take(program.NativeActorCount).ToArray();
    }

    private string? UnsupportedNativePolygonColor(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.POLYCOLOR or Jsm.Opcode.POLYCOLORALL))
        {
            return null;
        }
        if (fiber.Stack.Count < 3)
        {
            return "Native polygon color requires its original three DWORD RGB operands";
        }
        try
        {
            int[] bodies = NativePolygonColorBodies();
            if (operation.Opcode == Jsm.Opcode.POLYCOLOR && !bodies.Contains(fiber.ObjectId))
            {
                return "Native POLYCOLOR requires the caller's current612-byte physical body; a script/door record is not that actor";
            }
            if (bodies.Any(id => actors[id].PolygonColor is not Vector3 color || !ValidNativePolygonColor(color)))
            {
                return "Native polygon color has missing or malformed allocated RGB bytes";
            }
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
        return null;
    }

    private int[] ExecuteNativePolygonColor(Fiber fiber, bool all)
    {
        int[] targets = all ? NativePolygonColorBodies() : [fiber.ObjectId];
        int[] args = Pop(fiber, 3);
        Vector3 color = new(unchecked((byte)args[0]), unchecked((byte)args[1]), unchecked((byte)args[2]));
        foreach (int id in targets)
        {
            //526B00/526B80 own BYTE394..396.532340(16) overwrites every primitive RGB of this instance, not its shared resource.
            actors[id].PolygonColor = color;
        }
        return args;
    }

    private static bool ValidNativePolygonColor(Vector3 color)
    {
        return new[] { color.X, color.Y, color.Z }.All(value => float.IsFinite(value) && value >= byte.MinValue && value <= byte.MaxValue && value == MathF.Truncate(value));
    }
}
