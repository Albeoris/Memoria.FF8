namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleCameraState? NativeCamera { get; private set; }
    public byte NativeCameraTaskMask => unchecked((byte)(NativeCamera ?? throw new NotSupportedException("Native camera completion requires the original two-slot camera task owner.")).Mask);

    private void InitializeNativeStageCamera()
    {
        if (definition.NativeStageCameraBank is null)
        {
            return;
        }
        if (definition.NativeStageCameraSelectors.IsDefault || definition.NativeStageCameraSelectors.Length != 2 || definition.NativeStageCameraControl.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native stage camera requires both actual scene selector bytes.");
        }
        ushort roll = NextNativeGlobalRandom();
        byte selector = definition.NativeStageCameraSelectors[definition.EncounterId == 33 ? 0 : roll & 1];
        NativeCamera = NativeCamera! with { Variables = NativeCamera.Variables.SetItem(0, selector), ControlCursor = 0 };
    }

    internal void StartNativeCamera(NativeBattleCameraBank bank, byte selector)
    {
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native camera dispatch requires its initialized camera task owner.")).Start(bank, selector);
    }

    internal void AdvanceNativeCamera(uint graphicsFlags)
    {
        if (NativeCamera is not null)
        {
            if ((graphicsFlags & 0x101) == 0)
            {
                NativeCamera = NativeCamera.AdvanceControl(definition.NativeStageCameraBank!, definition.NativeStageCameraControl, new() { GlobalRandom = NextNativeGlobalRandom, ReadExternal = ReadNativeCameraExternal, ScheduleBlend = ScheduleNativeCameraBlend });
            }
            NativeCamera = NativeCamera.Tick(graphicsFlags);
        }
    }

    private void ScheduleNativeCameraBlend(byte duration)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeGraphics = graphics with { Auxiliary = graphics.Auxiliary.AppendCameraBlend(duration) };
    }

    private NativeBattleGraphicsState AdvanceNativeAuxiliary(NativeBattleGraphicsState graphics)
    {
        NativeBattleAuxiliaryStep step = graphics.Auxiliary.Advance();
        if (step.CameraBlend is ushort blend)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native509930 requires its initialized camera owner.")) with { BlendProgress = blend };
        }
        return graphics with { Auxiliary = step.State };
    }

    private void RestoreNativeCamera(BattleSnapshot snapshot)
    {
        if ((snapshot.NativeCamera is null) != (definition.NativeStageCameraBank is null))
        {
            throw new InvalidDataException("Native battle camera task owner is missing; migrate the snapshot explicitly.");
        }
        snapshot.NativeCamera?.Validate();
        if (snapshot.NativeCamera?.ControlCursor is int cursor && (definition.NativeStageCameraControl.IsDefault || cursor >= definition.NativeStageCameraControl.Length))
        {
            throw new InvalidDataException("Native camera control cursor exceeds its saved original program.");
        }
        NativeCamera = snapshot.NativeCamera;
    }

    private int ReadNativeCameraExternal(byte variable)
    {
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new NotSupportedException("Native camera participant queries require the actual loaded model bank.");
        return variable switch
        {
            19 => graphics.Models.Take(3).Count(model => (model.Flags0 & 2) != 0 && ((model.Flags8 ?? throw new InvalidDataException("Native camera requires loaded model status flags.")) & 2) == 0),
            27 => graphics.Models.Count(model => (model.Flags0 & 2) != 0 && (model.Flags8 ?? throw new InvalidDataException("Native camera requires loaded model status flags.")) == 0),
            _ => throw new NotSupportedException($"Native camera variable{variable} requires its active command descriptor owner.")
        };
    }
}
