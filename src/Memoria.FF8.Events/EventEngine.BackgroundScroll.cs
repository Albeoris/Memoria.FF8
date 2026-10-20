using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record BackgroundScrollSettings([property: JsonRequired] short OffsetX, [property: JsonRequired] short OffsetY, [property: JsonRequired] short ScaleX, [property: JsonRequired] short ScaleY);
public sealed record BackgroundScrollPosition([property: JsonRequired] short? X, [property: JsonRequired] short? Y, short TargetX, short TargetY, [property: JsonRequired] short? PreviousX, [property: JsonRequired] short? PreviousY, [property: JsonRequired] short? Counter, byte Mode, byte Status, [property: JsonRequired] short? Duration = null, byte? NativeActorId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int NativeLinearVersion = 0);
public sealed record NativeLinearScrollQuery(int Layer, short StartX, short StartY, short TargetX, short TargetY, short Duration);

public sealed partial class EventEngine
{
    public Func<int, BackgroundScrollSettings, bool>? CanSetBackgroundScroll { get; set; }
    public Func<int, BackgroundScrollSettings, bool>? CanSetBackgroundScrollRatio { get; set; }
    public Func<int, short, short, bool>? CanSetBackgroundScrollPosition { get; set; }
    public Func<int, byte, short, bool>? CanSetPartyBackgroundScroll { get; set; }
    public Func<int, short, short, short, bool>? CanSetCosineBackgroundScroll { get; set; }
    public Func<NativeLinearScrollQuery, bool>? CanSetLinearBackgroundScroll { get; set; }
    public Func<NativeLinearScrollQuery, SceneEventState, bool>? CanBeginLinearBackgroundScroll { get; set; }
    public Func<SceneEventState, bool>? CanRestoreLinearBackgroundScroll { get; set; }

    private string? UnsupportedLinearBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 6 || (uint)top[5] >= 8 || (short)top[0] < 0)
        {
            return "Native LSCROLL3 requires six original operands, one of eight layers and a supported signed-word duration";
        }
        NativeLinearScrollQuery query = new(top[5], (short)top[4], (short)top[3], (short)top[2], (short)top[1], (short)top[0]);
        if (!CanReplaceActorBackgroundScroll(query.Layer))
        {
            return "Native LSCROLL3 cannot replace unknown actor-scroll center history";
        }
        if (Scene.BackgroundScrollPositions.TryGetValue(query.Layer, out BackgroundScrollPosition? previous) && !ValidBackgroundScrollPosition(query.Layer, previous, Scene))
        {
            return "Native LSCROLL3 cannot replace an unknown saved scroll phase";
        }
        return CanSetLinearBackgroundScroll?.Invoke(query) == true && (CanBeginLinearBackgroundScroll is null || CanBeginLinearBackgroundScroll(query, Scene)) ? null : "Native LSCROLL3 needs certified original layer placement and wrapping before mutation";
    }

    private int[] ExecuteLinearBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 6);
        BackgroundScrollPosition previous = TakeBackgroundScrollPosition(args[0]);
        // 521530 writes current start8/10 and target16/18. Previous12/14
        // and counter6 remain until post-VM 475DF0 snapshots/resets them.
        Scene.BackgroundScrollPositions[args[0]] = previous with { X = (short)args[1], Y = (short)args[2], TargetX = (short)args[3], TargetY = (short)args[4], Duration = (short)args[5], Mode = 4, Status = 0, NativeLinearVersion = 1 };
        Scene.PresentationCameraRevision++;
        if (args[0] > 0 && Scene.NativeBackgroundScrollCurrents is { } currents)
        {
            currents[args[0]] = new(1, (short)args[1], (short)args[2]);
        }
        return args;
    }

    private string? UnsupportedBackgroundScrollSync2(Fiber fiber)
    {
        if (fiber.Stack.Count == 0 || (uint)fiber.Stack.Peek() >= 8 || !(Scene.ActorBackgroundScroll is { } actor && actor.Layer == fiber.Stack.Peek()) && (!Scene.BackgroundScrollPositions.TryGetValue(fiber.Stack.Peek(), out BackgroundScrollPosition? state) || !ValidBackgroundScrollPosition(fiber.Stack.Peek(), state, Scene)))
        {
            return "Native SCROLLSYNC2 needs its original unconsumed layer operand and known scroll phase";
        }
        return null;
    }

    private int[] ExecuteBackgroundScrollSync2(Fiber fiber)
    {
        int layer = fiber.Stack.Peek();
        if (BackgroundScrollStatus(layer) != 2)
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
        return layer is int selected && BackgroundScrollStatus(selected) == 2;
    }

    private string? UnsupportedCosineBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 4 || (uint)top[3] >= 8 || (short)top[0] < 0)
        {
            return "Native CSCROLL2 needs its four original operands, one of eight layers and a supported signed-word duration";
        }
        if (!CanReplaceActorBackgroundScroll(top[3]))
        {
            return $"Native CSCROLL2 layer{top[3]} cannot replace unknown row0 actor-scroll current history";
        }
        if (KnownNativeLinearScrollStart(top[3]) is null)
        {
            return $"Native CSCROLL2 layer{top[3]} has no current producer for selected camera{Scene.CameraIndex}; normal receipt camera{Scene.NormalBackgroundScrollCenter?.CameraIndex}, frame{Scene.NormalBackgroundScrollCenter?.Frame}";
        }
        return CanSetCosineBackgroundScroll?.Invoke(top[3], (short)top[2], (short)top[1], (short)top[0]) == true ? null : "Native CSCROLL2 needs certified original scroll state and renderer before mutation";
    }

    private int[] ExecuteCosineBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 4);
        if ((short)args[3] == 0)
        {
            Scene.PresentationCameraRevision++;
        }
        BackgroundScrollPosition previous = TakeBackgroundScrollPosition(args[0]);
        //521090 writes only this full-DWORD row's targetWORDs, durationWORD,
        //mode5/status0.475DF0 owns the later previous/counter publication.
        Scene.BackgroundScrollPositions[args[0]] = previous with { TargetX = (short)args[1], TargetY = (short)args[2], Duration = (short)args[3], Mode = 5, Status = 0, NativeLinearVersion = 0 };
        return args;
    }

    private string? UnsupportedBackgroundScrollSync()
    {
        return Scene.ActorBackgroundScroll is { Layer: 0 } || Scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? state) && ValidBackgroundScrollPosition(0, state, Scene) ? null : "Native SCROLLSYNC needs its known original layer0 state";
    }

    private void ExecuteBackgroundScrollSync(Fiber fiber)
    {
        if (BackgroundScrollStatus(0) != 2)
        {
            // Native return1 yields without advancing the instruction pointer.
            fiber.Position--;
            fiber.WaitReason = WaitReason.BackgroundScroll;
        }
    }

    private string? UnsupportedPartyBackgroundScroll(Fiber fiber)
    {
        if (Scene.ActorBackgroundScroll is { Layer: not 0 } || !CanReplaceActorBackgroundScroll(0))
        {
            return "Native CSCROLLP cannot replace unknown actor-scroll center history";
        }
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 2 || (short)top[0] < 0 || (uint)top[1] >= 3 || program is null || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || Scene.PartyBindingMemory is not { Version: 1 })
        {
            return "Native CSCROLLP needs a retained original party actor and supported signed-word duration";
        }
        byte nativeActor = Scene.PartyBindingMemory.At(top[1]);
        if (nativeActor >= Scene.NativeActorObjectIds.Count || variables.TryGetValue(194 + top[1], out int alias) && alias != nativeActor)
        {
            return "Native CSCROLLP party binding disagrees with its save194 alias";
        }
        return CanSetPartyBackgroundScroll?.Invoke(top[1], nativeActor, (short)top[0]) == true ? null : "Native CSCROLLP needs the actual retained saved-party body, authentic current center and certified dynamic projection before mutation";
    }

    private int[] ExecutePartyBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        byte nativeActor = Scene.PartyBindingMemory!.At(args[0]);
        int objectId = Scene.NativeActorObjectIds[nativeActor];
        int rawObjectId = Enumerable.Range(0, program!.NativeObjects.Count).Single(index => program.NativeObjects[index] == objectId);
        // 520EF0 samples saved B194+slot once, writes mode2/status0 and
        // lowWORD duration, preserves current/previous/counter and returns2.
        BeginNativeActorBackgroundScroll(rawObjectId, objectId, nativeActor, args[0], mode: 2, duration: (short)args[1]);
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
        Scene.PresentationCameraRevision++;
        int layer = layerZero ? 0 : args[0];
        int x = args[layerZero ? 0 : 1];
        int y = args[layerZero ? 1 : 2];
        BackgroundScrollPosition previous = TakeBackgroundScrollPosition(layer);
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
                short duration = state.Duration!.Value;
                short counter = state.Counter!.Value;
                short x = NativeLinearScrollPhase.Interpolate(state.PreviousX!.Value, state.TargetX, duration, counter);
                short y = NativeLinearScrollPhase.Interpolate(state.PreviousY!.Value, state.TargetY, duration, counter);
                Scene.BackgroundScrollPositions[layer] = state with { X = x, Y = y, Counter = counter == duration ? counter : (short)(counter + 1), Status = counter == duration ? (byte)2 : (byte)1 };
                continue;
            }
            if (state.Mode == 5 && state.Status is 0 or 1)
            {
                if (!ValidBackgroundScrollPosition(layer, state, Scene))
                {
                    throw new NotSupportedException("Native cosine layer scroll requires its consumed current, duration and phase words.");
                }
                if (state.Status == 0)
                {
                    state = state with { PreviousX = state.X, PreviousY = state.Y, Counter = 0, Status = 1 };
                }
                int targetX = state.TargetX;
                int targetY = state.TargetY;
                short duration = state.Duration!.Value;
                short counter = state.Counter!.Value;
                short x = (short)FaceCosineInterpolate(state.PreviousX!.Value, targetX, duration, counter);
                short y = (short)FaceCosineInterpolate(state.PreviousY!.Value, targetY, duration, counter);
                Scene.BackgroundScrollPositions[layer] = state with { X = x, Y = y, Counter = counter == duration ? counter : (short)(counter + 1), Status = counter == duration ? (byte)2 : (byte)1 };
                continue;
            }
            if (state.Mode == 3 && state.Status == 0)
            {
                // Native475DF0 runs after all actor VMs: direct mode records
                // commit their target in this update without interpolation.
                Scene.BackgroundScrollPositions[layer] = NativeDirectScrollPhase.Commit(state);
            }
        }
    }

    private bool ValidBackgroundScrollPosition(int layer, BackgroundScrollPosition state, SceneEventState scene)
    {
        if ((uint)layer >= 8 || state.X.HasValue != state.Y.HasValue || state.PreviousX.HasValue != state.PreviousY.HasValue)
        {
            return false;
        }
        if (state.Mode == 4)
        {
            short? startX = state.Status == 0 ? state.X : state.PreviousX;
            short? startY = state.Status == 0 ? state.Y : state.PreviousY;
            return NativeLinearScrollPhase.IsValid(state) && startX is short x && startY is short y && state.Duration is short duration && CanSetLinearBackgroundScroll?.Invoke(new NativeLinearScrollQuery(layer, x, y, state.TargetX, state.TargetY, duration)) == true;
        }
        if (state.NativeLinearVersion != 0)
        {
            return false;
        }
        if (state.Mode == 3)
        {
            return state.Status is 0 or 2 && (state.Status == 0 || state.X == state.TargetX && state.Y == state.TargetY && state.Counter == 0);
        }
        if (state.Mode != 5 || state.Status is not (0 or 1 or 2) || state.Duration is not short cosineDuration || cosineDuration < 0 || state.X is null || state.Y is null)
        {
            return false;
        }
        if (state.Status == 0)
        {
            return true;
        }
        if (state.Counter is not short cosineCounter || state.PreviousX is not short previousX || state.PreviousY is not short previousY || cosineCounter < 0 || cosineCounter > cosineDuration || state.Status == 2 && cosineCounter != cosineDuration || state.Status == 1 && cosineCounter == 0)
        {
            return false;
        }
        int elapsed = state.Status == 2 ? cosineCounter : cosineCounter - 1;
        int targetX = state.TargetX;
        int targetY = state.TargetY;
        return state.X == (short)FaceCosineInterpolate(previousX, targetX, cosineDuration, elapsed) && state.Y == (short)FaceCosineInterpolate(previousY, targetY, cosineDuration, elapsed);
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

    private void InitializeNativeBackgroundScrollSettings()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return;
        }
        // Executed normal52B4F0 entry1/3 writes all eight records, not a constructor or snapshot default.
        for (int layer = 0; layer < 8; layer++)
        {
            Scene.BackgroundScrollLayers.Add(layer, new BackgroundScrollSettings(0, 0, 256, 256));
        }
    }

    private string? UnsupportedBackgroundScrollRatio(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 3 || (uint)top[2] >= 8)
        {
            return "Native SCROLLRATIO2 requires three original operands and a full DWORD index into eight scroll records";
        }
        if (!Scene.BackgroundScrollLayers.TryGetValue(top[2], out BackgroundScrollSettings? previous))
        {
            return "Native SCROLLRATIO2 needs executed retained offset20/22 authority; unknown offsets are preserved";
        }
        BackgroundScrollSettings next = previous with { ScaleX = unchecked((short)top[1]), ScaleY = unchecked((short)top[0]) };
        return CanSetBackgroundScrollRatio?.Invoke(top[2], next) == true ? null : "Native SCROLLRATIO2 needs certified signed-WORD layer placement before changing only its ratio words";
    }

    private int[] ExecuteBackgroundScrollRatio(Fiber fiber)
    {
        int[] args = Pop(fiber, 3);
        Scene.PresentationCameraRevision++;
        BackgroundScrollSettings previous = Scene.BackgroundScrollLayers[args[0]];
        // 521460 writes only signedWORD24/26; offset20/22 and motion0..18 are untouched.
        Scene.BackgroundScrollLayers[args[0]] = previous with { ScaleX = unchecked((short)args[1]), ScaleY = unchecked((short)args[2]) };
        return args;
    }

    private int[] ExecuteBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 5);
        Scene.PresentationCameraRevision++;
        Scene.BackgroundScrollLayers[args[0]] = new BackgroundScrollSettings((short)args[1], (short)args[2], (short)args[3], (short)args[4]);
        return args;
    }
}
