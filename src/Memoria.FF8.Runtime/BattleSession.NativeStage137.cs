using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeStage137(NativeBattleGraphicsState graphics, NativeBattleStageState stage, NativeBattleStageTask task)
    {
        if (task.Phase == 1)
        {
            graphics = DrawNativeStandardStage(graphics);
            stage = NativeStage!;
        }
        NativeBattleStageExtendedState extended = stage.Extended ?? throw new NotSupportedException("Native50DFF0 requires its retained stage process.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native50DFF0 requires its retained camera process.");
        short shake = camera.ScreenOffsetScratchY ?? (camera.ProjectionOffsetsCleared ? (short)0 : throw new NotSupportedException("Native50DFF0 requires its screen-offset scratch WORD."));
        NativeBattleVector capturedEye = default;
        if (task.Phase == 1 && (stage.Flags & 8) == 0 && !extended.Stage137.Deforming && camera.Mask == 0 && (graphics.Flags & 0x8000) == 0)
        {
            NativeBattleCameraPoint eye = (camera.CapturedPose ?? throw new NotSupportedException("Native50E300 requires its captured camera pose.")).Eye;
            capturedEye = new(eye.X, eye.Y, eye.Z);
        }
        NativeBattleStage137Step step = NativeBattleStage137.Advance50DFF0(extended.Stage137, task.Phase, stage.Flags, stage.RotationSpeed, shake, unchecked((ushort)graphics.Flags), camera.Mask, capturedEye, () => NextNativeGlobalRandom(), DrawCrt);
        NativeStage = stage with { Extended = extended with { Stage137 = step.State }, Task = task with { Phase = step.Phase }, RotationSpeed = step.RotationSpeed };
        if (step.CameraShake != shake)
        {
            NativeCamera = camera with { ScreenOffsetScratchY = step.CameraShake, ProjectionOffsetsCleared = false };
        }
        if (step.DeformTick is int tick)
        {
            ImmutableArray<NativeBattleRawMesh> meshes = NativeStage.MutableGeometry ?? throw new NotSupportedException("Native50E120 requires its retained writable stage geometry.");
            NativeStage = NativeStage with { MutableGeometry = meshes.Select(mesh => mesh.StageGroup is 0 or 1 or 2 ? NativeBattleStage137.Deform50E120(mesh, mesh.StageGroup == 2 ? tick : tick / 2) : mesh).ToImmutableArray() };
        }
        if (step.CameraEye is { } position)
        {
            NativeBattleCameraPose pose = NativeCamera!.CurrentPose ?? throw new NotSupportedException("Native50E300 requires its current camera pose.");
            NativeCamera = NativeCamera with { CurrentPose = pose with { Eye = pose.Eye with { X = position.X, Y = position.Y } } };
        }
        if (task.Phase == 1 && (stage.Flags & 2) == 0 && NativeStage.TextureAnimations is not null)
        {
            AdvanceNativeStageTexture(0);
        }
        return graphics;

        int DrawCrt()
        {
            NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native50E1C0 requires its CRT random owner.");
            nativeCrtRandom = random.Draw(out ushort value);
            return value;
        }
    }
}
