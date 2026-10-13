using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record BackgroundScrollSettings(short OffsetX, short OffsetY, short ScaleX, short ScaleY);
public sealed record BackgroundScrollPosition(short X, short Y, short TargetX, short TargetY, short PreviousX, short PreviousY, short Counter, byte Mode, byte Status, short Duration = 0, byte? NativeActorId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int NativeLinearVersion = 0);
public sealed record NativeLinearScrollQuery(int Layer, short StartX, short StartY, short TargetX, short TargetY, short Duration);

public sealed partial class EventEngine
{
    public Func<int, BackgroundScrollSettings, bool>? CanSetBackgroundScroll { get; set; }
    public Func<int, short, short, bool>? CanSetBackgroundScrollPosition { get; set; }
    public Func<int, byte, short, bool>? CanSetPartyBackgroundScroll { get; set; }
    public Func<int, short, short, short, bool>? CanSetCosineBackgroundScroll { get; set; }
    public Func<NativeLinearScrollQuery, bool>? CanSetLinearBackgroundScroll { get; set; }
    public Func<NativeLinearScrollQuery, SceneEventState, bool>? CanBeginLinearBackgroundScroll { get; set; }
    public Func<SceneEventState, bool>? CanRestoreLinearBackgroundScroll { get; set; }

    private bool IsNativeLinearScrollLoopJump(Fiber fiber, JsmOperation operation)
    {
        // TODO(TD-004): replace this scoped loop correction with the shared native scheduler budget
        // 51C4D0 returns4; native529FF0 continues within the same16-op
        // budget. Admit only this proved completed135 lava-loop request.
        return FieldId == 135 && programHash == "4E838898400B0B44D1FFAD537A77FDD685111A7C25DF34366CD77CACE8A65344" && fiber.ObjectId == 13 && fiber.Script.Id == 53 && operation.Index == 315 && operation.Parameter == -24 && Scene.NativeFieldLoadVersion == 1 && Scene.NativeActorLayoutVersion == 1 && Scene.BackgroundScrollPositions.TryGetValue(1, out BackgroundScrollPosition? state) && state is { Mode: 4, NativeLinearVersion: 1, Status: 2, PreviousX: 0, PreviousY: 0, TargetX: 351, TargetY: 0, Duration: 351 } && ValidBackgroundScrollPosition(1, state, Scene);
    }

    private string? UnsupportedLinearBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 6 || (uint)top[5] >= 8 || (short)top[0] < 0)
        {
            return "Native LSCROLL3 requires six original operands, one of eight layers and a supported signed-word duration";
        }
        NativeLinearScrollQuery query = new(top[5], (short)top[4], (short)top[3], (short)top[2], (short)top[1], (short)top[0]);
        if (Scene.BackgroundScrollPositions.TryGetValue(query.Layer, out BackgroundScrollPosition? previous) && !ValidBackgroundScrollPosition(query.Layer, previous, Scene))
        {
            return "Native LSCROLL3 cannot replace an unknown saved scroll phase";
        }
        return CanSetLinearBackgroundScroll?.Invoke(query) == true && (CanBeginLinearBackgroundScroll is null || CanBeginLinearBackgroundScroll(query, Scene)) ? null : "Native LSCROLL3 needs certified original layer placement and wrapping before mutation";
    }

    private int[] ExecuteLinearBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 6);
        BackgroundScrollPosition previous = Scene.BackgroundScrollPositions.GetValueOrDefault(args[0]) ?? new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 0, 1);
        // 521530 writes current start8/10 and target16/18. Previous12/14
        // and counter6 remain until post-VM 475DF0 snapshots/resets them.
        Scene.BackgroundScrollPositions[args[0]] = previous with { X = (short)args[1], Y = (short)args[2], TargetX = (short)args[3], TargetY = (short)args[4], Duration = (short)args[5], Mode = 4, Status = 0, NativeLinearVersion = 1 };
        return args;
    }

    private string? UnsupportedBackgroundScrollSync2(Fiber fiber)
    {
        if (fiber.Stack.Count == 0 || (uint)fiber.Stack.Peek() >= 8 || !Scene.BackgroundScrollPositions.TryGetValue(fiber.Stack.Peek(), out BackgroundScrollPosition? state) || !ValidBackgroundScrollPosition(fiber.Stack.Peek(), state, Scene))
        {
            return "Native SCROLLSYNC2 needs its original unconsumed layer operand and known scroll phase";
        }
        return null;
    }

    private int[] ExecuteBackgroundScrollSync2(Fiber fiber)
    {
        int layer = fiber.Stack.Peek();
        if (Scene.BackgroundScrollPositions[layer].Status != 2)
        {
            // 520F60 peeks the DWORD layer and returns1 without popping.
            fiber.Position--;
            fiber.WaitReason = WaitReason.BackgroundScroll;
            return [];
        }
        return Pop(fiber, 1);
    }

    private bool BackgroundScrollWaitReady(Fiber fiber)
    {
        int? layer = program!.Operations[fiber.Position].Opcode switch
        {
            Jsm.Opcode.SCROLLSYNC => 0,
            Jsm.Opcode.SCROLLSYNC2 when fiber.Stack.Count > 0 => fiber.Stack.Peek(),
            _ => null
        };
        return layer is int selected && Scene.BackgroundScrollPositions.TryGetValue(selected, out BackgroundScrollPosition? state) && state.Status == 2;
    }

    private string? UnsupportedCosineBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 4 || (uint)top[3] >= 8 || (short)top[0] < 0)
        {
            return "Native CSCROLL2 needs its four original operands, one of eight layers and a supported signed-word duration";
        }
        return CanSetCosineBackgroundScroll?.Invoke(top[3], (short)top[2], (short)top[1], (short)top[0]) == true ? null : "Native CSCROLL2 needs certified original scroll state and renderer before mutation";
    }

    private int[] ExecuteCosineBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 4);
        BackgroundScrollPosition previous = Scene.BackgroundScrollPositions.GetValueOrDefault(args[0]) ?? new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 0, 1);
        Scene.BackgroundScrollPositions[args[0]] = previous with { TargetX = (short)args[1], TargetY = (short)args[2], Duration = (short)args[3], Mode = 5, Status = 0, NativeLinearVersion = 0 };
        return args;
    }

    private string? UnsupportedBackgroundScrollSync()
    {
        return Scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? state) && ValidBackgroundScrollPosition(0, state, Scene) ? null : "Native SCROLLSYNC needs its known original layer0 state";
    }

    private void ExecuteBackgroundScrollSync(Fiber fiber)
    {
        if (Scene.BackgroundScrollPositions[0].Status != 2)
        {
            // Native return1 yields without advancing the instruction pointer.
            fiber.Position--;
            fiber.WaitReason = WaitReason.BackgroundScroll;
        }
    }

    private string? UnsupportedPartyBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 2 || Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || (uint)top[1] >= 3 || ResolvePartyActor(top[1]) is null || (short)top[0] < 0)
        {
            return "Native CSCROLLP needs a retained original party actor and supported signed-word duration";
        }
        byte nativeActor = (byte)Scene.FieldPartyNativeActorIds[top[1]];
        if (variables.TryGetValue(194 + top[1], out int alias) && (byte)alias != nativeActor)
        {
            return "Native CSCROLLP party binding disagrees with its save194 alias";
        }
        return CanSetPartyBackgroundScroll?.Invoke(top[1], nativeActor, (short)top[0]) == true ? null : "Native CSCROLLP needs a certified projected/clamped zero target and renderer before mutation";
    }

    private int[] ExecutePartyBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        BackgroundScrollPosition previous = Scene.BackgroundScrollPositions.GetValueOrDefault(0) ?? new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 0, 1);
        Scene.BackgroundScrollPositions[0] = previous with { Duration = (short)args[1], NativeActorId = (byte)Scene.FieldPartyNativeActorIds[args[0]], Mode = 2, Status = 0, NativeLinearVersion = 0 };
        return args;
    }

    private string? UnsupportedDirectBackgroundScroll(Fiber fiber, bool layerZero = false)
    {
        int[] top = fiber.Stack.ToArray();
        int operands = layerZero ? 2 : 3;
        if (top.Length < operands || !layerZero && (uint)top[2] >= 8)
        {
            return "Native direct scroll requires its original operands and one of eight scroll records";
        }
        int layer = layerZero ? 0 : top[2];
        return CanSetBackgroundScrollPosition?.Invoke(layer, (short)top[1], (short)top[0]) == true ? null : "Native direct scroll needs proved current scroll state and renderer offsets before mutation";
    }

    private int[] ExecuteDirectBackgroundScroll(Fiber fiber, bool layerZero = false)
    {
        int[] args = Pop(fiber, layerZero ? 2 : 3);
        int layer = layerZero ? 0 : args[0];
        int x = args[layerZero ? 0 : 1];
        int y = args[layerZero ? 1 : 2];
        BackgroundScrollPosition previous = Scene.BackgroundScrollPositions.GetValueOrDefault(layer) ?? new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 0, 1);
        Scene.BackgroundScrollPositions[layer] = previous with { TargetX = (short)x, TargetY = (short)y, Mode = 3, Status = 0, NativeLinearVersion = 0 };
        // Native return3 advances PC and yields its instruction budget.
        WaitFrames(fiber, 1);
        return args;
    }

    private void StepNativeBackgroundScroll()
    {
        foreach ((int layer, BackgroundScrollPosition saved) in Scene.BackgroundScrollPositions.ToArray())
        {
            BackgroundScrollPosition state = saved;
            if (state.Mode == 4 && state.Status is 0 or 1)
            {
                if (!ValidBackgroundScrollPosition(layer, state, Scene))
                {
                    throw new NotSupportedException("Native linear layer scroll has an unproved phase or renderer contract.");
                }
                if (state.Status == 0)
                {
                    state = state with { PreviousX = state.X, PreviousY = state.Y, Counter = 0, Status = 1 };
                }
                short x = NativeLinearScrollPhase.Interpolate(state.PreviousX, state.TargetX, state.Duration, state.Counter);
                short y = NativeLinearScrollPhase.Interpolate(state.PreviousY, state.TargetY, state.Duration, state.Counter);
                Scene.BackgroundScrollPositions[layer] = state with { X = x, Y = y, Counter = state.Counter == state.Duration ? state.Counter : (short)(state.Counter + 1), Status = state.Counter == state.Duration ? (byte)2 : (byte)1 };
                continue;
            }
            if (state.Mode is 2 or 5 && state.Status is 0 or 1)
            {
                if (state.Status == 0)
                {
                    state = state with { PreviousX = state.X, PreviousY = state.Y, Counter = 0, Status = 1 };
                }
                // Mode2's host certificate proves the projected actor is clamped to zero.
                int targetX = state.Mode == 2 ? 0 : state.TargetX;
                int targetY = state.Mode == 2 ? 0 : state.TargetY;
                short x = (short)FaceCosineInterpolate(state.PreviousX, targetX, state.Duration, state.Counter);
                short y = (short)FaceCosineInterpolate(state.PreviousY, targetY, state.Duration, state.Counter);
                Scene.BackgroundScrollPositions[layer] = state with { X = x, Y = y, Counter = state.Counter == state.Duration ? state.Counter : (short)(state.Counter + 1), Status = state.Counter == state.Duration ? (byte)2 : (byte)1 };
                continue;
            }
            if (state.Mode == 3 && state.Status == 0)
            {
                // Native475DF0 runs after all actor VMs: direct mode records
                // commit their target in this update without interpolation.
                Scene.BackgroundScrollPositions[layer] = state with { PreviousX = state.X, PreviousY = state.Y, Counter = 0, X = state.TargetX, Y = state.TargetY, Status = 2 };
            }
        }
    }

    private bool ValidBackgroundScrollPosition(int layer, BackgroundScrollPosition state, SceneEventState scene)
    {
        if ((uint)layer >= 8)
        {
            return false;
        }
        if (state.Mode == 4)
        {
            short startX = state.Status == 0 ? state.X : state.PreviousX;
            short startY = state.Status == 0 ? state.Y : state.PreviousY;
            return NativeLinearScrollPhase.IsValid(state) && CanSetLinearBackgroundScroll?.Invoke(new NativeLinearScrollQuery(layer, startX, startY, state.TargetX, state.TargetY, state.Duration)) == true;
        }
        if (state.NativeLinearVersion != 0)
        {
            return false;
        }
        if (state.Mode == 3)
        {
            return state.Status is 0 or 2 && (state.Status == 0 || state.X == state.TargetX && state.Y == state.TargetY && state.Counter == 0);
        }
        if (state.Mode is not (2 or 5) || state.Status is not (0 or 1 or 2) || state.Duration < 0 || state.Mode == 2 && (layer != 0 || state.NativeActorId is not byte actor || actor == 0xFF || scene.NativeActorLayoutVersion != 1 || actor >= scene.NativeActorObjectIds.Count))
        {
            return false;
        }
        if (state.Status == 0)
        {
            return true;
        }
        if (state.Counter < 0 || state.Counter > state.Duration || state.Status == 2 && state.Counter != state.Duration || state.Status == 1 && state.Counter == 0)
        {
            return false;
        }
        int elapsed = state.Status == 2 ? state.Counter : state.Counter - 1;
        int targetX = state.Mode == 2 ? 0 : state.TargetX;
        int targetY = state.Mode == 2 ? 0 : state.TargetY;
        return state.X == (short)FaceCosineInterpolate(state.PreviousX, targetX, state.Duration, elapsed) && state.Y == (short)FaceCosineInterpolate(state.PreviousY, targetY, state.Duration, elapsed);
    }

    private string? UnsupportedBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 5 || (uint)top[4] >= 8)
        {
            return "Native SCROLLMODE2 requires its original five operands and one of eight scroll records";
        }
        BackgroundScrollSettings settings = new((short)top[3], (short)top[2], (short)top[1], (short)top[0]);
        return CanSetBackgroundScroll?.Invoke(top[4], settings) == true ? null : "Native SCROLLMODE2 needs a proved renderer for the selected signed-word offsets/scales and camera state";
    }

    private int[] ExecuteBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 5);
        Scene.BackgroundScrollLayers[args[0]] = new BackgroundScrollSettings((short)args[1], (short)args[2], (short)args[3], (short)args[4]);
        return args;
    }
}
