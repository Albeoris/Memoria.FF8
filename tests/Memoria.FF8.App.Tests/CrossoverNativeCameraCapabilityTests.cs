using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeCameraCapabilityTests
{
    [Fact]
    public void Registering_an_original_control_resource_does_not_acquire_a_host_camera_lease()
    {
        NativeBattleCameraState host = NativeBattleCameraState.FromColdProcess();
        NativeBattleEffectAnimationRegisters resource = new(new("Camera", [0, 0, 6, 0, 8, 0, 2, 0, 0, 0]), 0, 6, 8);
        NativeBattleCameraState registered = host.RegisterControlResource(resource);
        Assert.Equal(0, registered.ControlCursor);
        Assert.Same(resource, registered.ControlResource);
        NativeBattleCameraState held = BattleSession.HoldEffectPresentationCamera(registered, host);
        Assert.Same(resource, held.ControlResource);
        Assert.Null(held.ControlCursor);
        Assert.Equal(host.Projection, held.Projection);
    }

    [Fact]
    public void An_actual_native_camera_track_is_acknowledged_without_moving_the_host_camera()
    {
        NativeBattleCameraState host = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraState requested = host.Start(new([1, 0, 4, 0, 2, 0, 0, 0, 0, 0]), 0);
        Assert.Contains(requested.Tasks, task => task is not null);
        NativeBattleCameraState held = BattleSession.HoldEffectPresentationCamera(requested, host);
        Assert.All(held.Tasks, task => Assert.Null(task));
        Assert.Equal(host.CurrentPose, held.CurrentPose);
        Assert.Equal(host.Projection, held.Projection);
        Assert.Equal(0, held.Mask);
    }

    [Fact]
    public void Native_pose_and_focal_writes_leave_the_host_projection_unchanged()
    {
        NativeBattleCameraState host = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraPose original = host.CurrentPose!;
        NativeBattleCameraPose moved = original with { Eye = original.Eye with { X = 1 } };
        Assert.Equal(host.CurrentPose, BattleSession.HoldEffectPresentationCamera(host with { CurrentPose = moved }, host).CurrentPose);
        Assert.Equal(host.FocalLength, BattleSession.HoldEffectPresentationCamera(host with { FocalLength = 512 }, host).FocalLength);
        Assert.Equal(host.Projection, BattleSession.HoldEffectPresentationCamera(host with { Projection = host.Projection! with { FocalLength = 512 } }, host).Projection);
    }
}
