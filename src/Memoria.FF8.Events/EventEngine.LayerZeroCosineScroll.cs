namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedLayerZeroCosineBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 3 || (short)top[0] < 0)
        {
            return "Native CSCROLL requires its three original operands and a supported signed-word duration";
        }
        if (!CanReplaceActorBackgroundScroll(0) || KnownNativeLayerZeroScrollCenter() is null)
        {
            return "Native CSCROLL cannot replace unknown actor-scroll center history";
        }
        return CanSetCosineBackgroundScroll?.Invoke(0, (short)top[2], (short)top[1], (short)top[0]) == true ? null : "Native CSCROLL needs certified original layer0 scroll state and renderer before mutation";
    }

    private int[] ExecuteLayerZeroCosineBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 3);
        BackgroundScrollPosition previous = TakeBackgroundScrollPosition(0);
        // 520CF0 writes layer0 target16/18, duration4, mode5/status0 and
        // returns2. The shared475DF0 driver retains the current position
        // until it snapshots it and publishes cosine counter0 post-VM.
        Scene.BackgroundScrollPositions[0] = previous with { TargetX = (short)args[0], TargetY = (short)args[1], Duration = (short)args[2], Mode = 5, Status = 0, NativeLinearVersion = 0 };
        return args;
    }
}
