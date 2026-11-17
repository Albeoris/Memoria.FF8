using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectPresentationCameraTests
{
    [Theory]
    [InlineData((ushort)216, (ushort)512)]
    [InlineData((ushort)512, (ushort)333)]
    public void Host_projection_refresh_supplies_the_focal_WORD_consumed_by_ground_capture_and_retains_camera_ownership(ushort firstFocal, ushort nextFocal)
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraProjection first = new(NativeBattleForeignModelGeometry.Identity, firstFocal, 160, 108);
        NativeBattleCameraState host = BattlePresentationProcess.BindEffectPresentationHostProjection(cold, first);
        Assert.Equal(unchecked((short)firstFocal), host.FocalLength);
        NativeBattleCameraProjection next = first with { FocalLength = nextFocal };
        host = BattlePresentationProcess.BindEffectPresentationHostProjection(host, next);
        NativeBattleCameraState held = BattlePresentationProcess.HoldEffectPresentationCamera(host with { FocalLength = 999, Projection = first }, host);
        NativeBattleThundaga105CaptureTransform capture = NativeBattleThundaga105CaptureGeometry.TransformCapture(new byte[8].ToImmutableArray(), new(new(-384, 0, -384), new(384, 0, 384)), held.Projection!, 128, 256, held.FocalLength!.Value);
        Assert.Same(next, held.Projection);
        Assert.Equal((int)nextFocal, capture.Matrix.Z);
        Assert.Equal(nextFocal, capture.Projection.FocalLength);
        Assert.Equal((short)0, cold.FocalLength);
        Assert.Equal(cold.CurrentPose, held.CurrentPose);
    }
}
