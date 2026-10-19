using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278CameraTests
{
    [Fact]
    public void First_window_sets_native_control_pose_focal_and_signed_orbit()
    {
        NativeBattleCameraState source = NativeBattleCameraState.Initialize() with { Mask = 0x1234, Roll = -99, FocalLength = -216, EyePaddingF6 = 17, TargetPaddingFE = 29 };
        NativeBattleCameraState result = NativeBattleCarbuncle278Camera.AdvanceFirstWindow(source, 100, 1);
        Assert.True(result.ForceCamera);
        Assert.Equal((ushort)0, result.Mask);
        Assert.Equal(new NativeBattleCameraPoint(-288, -3867, 3022), result.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(0, 0, 100), result.CurrentPose.Target);
        Assert.Equal((short)288, result.FocalLength);
        Assert.Equal((short)0, result.Roll);
        Assert.Equal((short)17, result.EyePaddingF6);
        Assert.Equal((short)29, result.TargetPaddingFE);
        Assert.Null(result.Projection);
        Assert.Null(source.CurrentPose);
    }

    [Fact]
    public void Orbit_uses_signed_differences_and_wraps_only_final_word_stores()
    {
        NativeBattleCameraPose source = new(new(30000, -30000, -30000), new(-30000, 30000, 30000), -12, 17);
        NativeBattleCameraPose result = NativeBattleCarbuncle278Camera.Orbit683D10(source, 1024, 4096);
        Assert.Equal(new NativeBattleCameraPoint(30000, -30000, 24464), result.Eye);
        Assert.Equal(source.Target, result.Target);
        Assert.Equal(source.FocalLength, result.FocalLength);
        Assert.Equal(source.Roll, result.Roll);
    }

    [Fact]
    public void Later_window_retains_scalar_owners_and_restores_independently()
    {
        NativeBattleCameraState source = NativeBattleCameraState.Initialize() with { CurrentPose = new(new(20, -30, 40), new(-10, 20, 100), 512, 0), FocalLength = -123, Roll = 17, Mask = 9 };
        NativeBattleCameraState first = NativeBattleCarbuncle278Camera.AdvanceFirstWindow(source, 200, 2);
        NativeBattleCameraState independent = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(source))!;
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(NativeBattleCarbuncle278Camera.AdvanceFirstWindow(independent, 200, 2)));
        Assert.Equal((short)-123, first.CurrentPose!.FocalLength);
        Assert.Equal((short)17, first.CurrentPose.Roll);
        Assert.Equal((ushort)9, first.Mask);
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Camera.AdvanceFirstWindow(source with { CurrentPose = null }, 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleCarbuncle278Camera.AdvanceFirstWindow(source, 0, 0));
    }
}
