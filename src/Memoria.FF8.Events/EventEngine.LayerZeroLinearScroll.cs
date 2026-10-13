namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private (short X, short Y)? KnownNativeLayerZeroScrollCenter()
    {
        if (Scene.ActorBackgroundScroll is { X: short x, Y: short y } actor && NativeActorCosineScrollPhase.IsValid(actor))
        {
            return (x, y);
        }
        if (Scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? position) && ValidBackgroundScrollPosition(0, position, Scene))
        {
            return (position.X, position.Y);
        }
        if (Scene.NormalBackgroundScrollCenter is { Version: 1 } normal && normal.FieldId == FieldId && normal.CameraIndex == Scene.CameraIndex && normal.Frame > 0 && normal.Frame <= Frame)
        {
            return (normal.X, normal.Y);
        }
        return null;
    }

    private string? UnsupportedNativeLayerZeroLinearBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 3 || (short)top[0] < 0 || KnownNativeLayerZeroScrollCenter() is not { } start || !CanReplaceActorBackgroundScroll(0))
        {
            return "Native LSCROLL requires three original operands, a signed-WORD duration and authentic current layer0 center";
        }
        NativeLinearScrollQuery query = new(0, start.X, start.Y, (short)top[2], (short)top[1], (short)top[0]);
        return CanSetLinearBackgroundScroll?.Invoke(query) == true && (CanBeginLinearBackgroundScroll is null || CanBeginLinearBackgroundScroll(query, Scene)) ? null : "Native LSCROLL requires certified shared layer0 placement and current camera history before mutation";
    }

    private int[] ExecuteNativeLayerZeroLinearBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 3);
        BackgroundScrollPosition previous = TakeBackgroundScrollPosition(0);
        // 520C90 preserves current8/10, previous12/14 and counter6, writes
        // target16/18 plus duration4/mode4/status0, and returns2.
        Scene.BackgroundScrollPositions[0] = previous with { TargetX = (short)args[0], TargetY = (short)args[1], Duration = (short)args[2], Mode = 4, Status = 0, NativeLinearVersion = 1 };
        return args;
    }
}
