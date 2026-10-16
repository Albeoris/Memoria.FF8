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
            NativeCamera = NativeCamera.Tick(graphicsFlags, new() { TransformReference = (reference, local) => TransformNativeCameraReference(reference, local, graphicsFlags) });
        }
    }

    private NativeBattleCameraPoint TransformNativeCameraReference(byte reference, NativeBattleCameraPoint local, uint graphicsFlags)
    {
        if (reference is 0xF2 or >= 0xF5 and <= 0xF9 || reference >= 0xB0)
        {
            NativeBattleActionGeometry context = RequireNativeGraphics().ActionContext ?? throw new NotSupportedException("Native50D790 matrix references require their captured50D300 action context.");
            NativeBattleActionMatrix matrix = NativeBattleCameraGraphics.ReadMatrix(context, reference);
            NativeBattleVector result = NativeBattlePoseMatrices.TransformCameraPoint(matrix, new(local.X, local.Y, local.Z), (graphicsFlags & 0x20) != 0);
            return new(result.X, result.Y, result.Z);
        }
        int model = reference >> 4;
        if (model is 9 or 10)
        {
            NativeBattleActionGeometry context = RequireNativeGraphics().ActionContext ?? throw new NotSupportedException("Native50D790 camera aliases require the actual50D300 command geometry owner.");
            model = model == 9 ? context.ActorIndex : context.TargetIndex ?? throw new NotSupportedException("Native camera target alias requires its resolved physical target packet.");
        }
        if (model >= 7)
        {
            throw new NotSupportedException($"Native camera reference{reference:X2} requires its active command or auxiliary matrix owner.");
        }
        return TransformNativeGraphicsCameraPoint(model, (byte)(reference & 15), local, (graphicsFlags & 0x20) != 0);
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
        return NativeBattleCameraGraphics.ReadVariable(graphics, variable);
    }
}
