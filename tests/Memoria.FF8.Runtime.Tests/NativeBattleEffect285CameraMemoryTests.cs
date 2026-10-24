using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect285CameraMemoryTests
{
    [Fact]
    public void Position_WORD_stores_retain_destination_padding_and_current_camera_scalars()
    {
        NativeBattleCameraState camera = Camera();
        NativeBattleEffect285CameraSnapshot captured = BattleSession.CaptureNativeEffect285Camera(camera);
        Assert.Throws<NotSupportedException>(() => captured.SavedEye8.Read(6, 2));
        Assert.Equal((short)14, BinaryPrimitives.ReadInt16LittleEndian(captured.SavedTarget8.Read(6, 2)));
        NativeBattleCameraState moved = BattleSession.ApplyNativeEffect285CameraPoint(camera, new(true, Point(4, 5, 6)));
        Assert.Equal(new NativeBattleCameraPoint(4, 5, 6), moved.CurrentPose!.Eye);
        Assert.Equal(camera.CurrentPose!.Target, moved.CurrentPose.Target);
        Assert.Equal((short)10, moved.EyePaddingF6);
        Assert.Equal((short)777, moved.FocalLength);
        Assert.Equal((short)123, moved.Roll);
        Assert.Equal(camera.ForceCamera, moved.ForceCamera);
        Assert.Same(camera.CapturedPose, moved.CapturedPose);
        Assert.Same(camera.Projection, moved.Projection);
    }

    [Fact]
    public void Saved_DWORD_restores_copy_padding_knownness_without_restoring_saved_roll_or_focal()
    {
        NativeBattleCameraState camera = Camera();
        NativeBattleEffect285CameraSnapshot captured = BattleSession.CaptureNativeEffect285Camera(camera);
        NativeBattleCameraState eye = BattleSession.ApplyNativeEffect285CameraPoint(camera, new(true, captured.SavedEye8, true));
        NativeBattleCameraState restored = BattleSession.ApplyNativeEffect285CameraPoint(eye, new(false, captured.SavedTarget8, true));
        Assert.Equal(camera.CapturedPose!.Eye, restored.CurrentPose!.Eye);
        Assert.Equal(camera.CapturedPose.Target, restored.CurrentPose.Target);
        Assert.Null(restored.EyePaddingF6);
        Assert.Equal((short)14, restored.TargetPaddingFE);
        Assert.Equal(camera.FocalLength, restored.FocalLength);
        Assert.Equal(camera.Roll, restored.Roll);
        Assert.Throws<NotSupportedException>(() => BattleSession.ApplyNativeEffect285CameraPoint(camera, new(true, captured.SavedEye8.Write(6, [1]), true)));
    }

    [Fact]
    public void Model_BYTE_stores_preserve_high_flags_and_WORD_stores_restore_only_selected_bits()
    {
        NativeBattleGraphicsModel model = new(3, 0xABFD);
        NativeBattleGraphicsModel cleared = BattleSession.ApplyNativeEffect285ModelFlags(model, new(0x1D97494, 1, 0xFB, 0));
        Assert.Equal((ushort)0xABF9, cleared.Flags0);
        NativeBattleGraphicsModel restored = BattleSession.ApplyNativeEffect285ModelFlags(cleared, new(0x1D97494, 2, 0xFFFB, 4));
        Assert.Equal(model.Flags0, restored.Flags0);
        Assert.Throws<NotSupportedException>(() => BattleSession.ApplyNativeEffect285ModelFlags(model, new(0x1D97494, 4, 0, 0)));
    }

    private static NativeBattleCameraState Camera() => NativeBattleCameraState.Initialize() with { CurrentPose = new(new(1, 2, 3), new(7, 8, 9), 777, 123), CapturedPose = new(new(-1, -2, -3), new(-7, -8, -9), 555, -5), FocalLength = 777, Roll = 123, EyePaddingF6 = 10, CapturedEyePadding806 = null, CapturedTargetPadding80E = 14, ForceCamera = true };
    private static NativeBattleEffectArena Point(short x, short y, short z) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
}
