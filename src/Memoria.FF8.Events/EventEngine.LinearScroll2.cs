using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private (short X, short Y)? KnownNativeLinearScrollStart(int layer)
    {
        if (Scene.ActorBackgroundScroll is { X: short actorX, Y: short actorY } retained && retained.Layer == layer && NativeActorScrollPhase.IsValid(retained))
        {
            return (actorX, actorY);
        }
        if (layer == 0)
        {
            return KnownNativeLayerZeroScrollCenter();
        }
        if (Scene.BackgroundScrollPositions.TryGetValue(layer, out BackgroundScrollPosition? previous))
        {
            return previous.X is short x && previous.Y is short y && ValidBackgroundScrollPosition(layer, previous, Scene) ? (x, y) : null;
        }
        if (layer == Scene.CameraIndex)
        {
            return Scene.NormalBackgroundScrollCenter is { Version: 1 } selected && selected.FieldId == FieldId && selected.CameraIndex == layer && selected.Frame > 0 && selected.Frame <= Frame ? (selected.X, selected.Y) : null;
        }
        //52B4F0 initializes mode0;475E60 visits all eight rows and zeros
        //each nonselected mode0 current independently of the selected mode.
        //A normal-center receipt or an executed actor-scroll phase proves
        //that callback; a newly issued status0 actor command proves nothing.
        if (Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || Scene.NativeFieldLoadVersion != 1 || Scene.CameraIndex is < 0 or > 1)
        {
            return null;
        }
        bool normalCallback = Scene.NormalBackgroundScrollCenter is { Version: 1 } normal && normal.CameraIndex == Scene.CameraIndex && normal.FieldId == FieldId && normal.Frame > 0 && normal.Frame <= Frame;
        bool actorCallback = Scene.ActorBackgroundScroll is { Version: 1, Status: 1 or 2 } actor && NativeActorScrollPhase.IsValid(actor);
        return normalCallback || actorCallback ? ((short)0, (short)0) : null;
    }

    private string? UnsupportedLinearBackgroundScroll2(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 4 || (uint)top[3] >= 8 || (short)top[0] < 0 || !CanReplaceActorBackgroundScroll(top[3]) || KnownNativeLinearScrollStart(top[3]) is not { } start)
        {
            return "Native LSCROLL2 requires four original operands, an allocated layer, supported signed-WORD duration and authentic current center";
        }
        NativeLinearScrollQuery query = new(top[3], start.X, start.Y, (short)top[2], (short)top[1], (short)top[0]);
        return CanSetLinearBackgroundScroll?.Invoke(query) == true && (CanBeginLinearBackgroundScroll is null || CanBeginLinearBackgroundScroll(query, Scene)) ? null : "Native LSCROLL2 requires the shared layer placement and current camera contract before mutation";
    }

    private int[] ExecuteLinearBackgroundScroll2(Fiber fiber)
    {
        int[] args = Pop(fiber, 4);
        BackgroundScrollPosition previous = TakeBackgroundScrollPosition(args[0]);
        // 521010 preserves current8/10, previous12/14, counter6 and actor1.
        // The shared post-VM driver snapshots the start before interpolation.
        Scene.BackgroundScrollPositions[args[0]] = previous with { TargetX = (short)args[1], TargetY = (short)args[2], Duration = (short)args[3], Mode = 4, Status = 0, NativeLinearVersion = 1 };
        return args;
    }
}
