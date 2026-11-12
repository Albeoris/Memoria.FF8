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
        BattleSession.ValidateEffectPresentationCamera(registered, host);
    }

    [Fact]
    public void An_actual_native_camera_track_requires_a_host_camera_lease()
    {
        NativeBattleCameraState host = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraState requested = host.Start(new([1, 0, 4, 0, 2, 0, 0, 0, 0, 0]), 0);
        Assert.Contains(requested.Tasks, task => task is not null);
        Assert.Throws<NotSupportedException>(() => BattleSession.ValidateEffectPresentationCamera(requested, host));
    }

    [Fact]
    public void Native_pose_or_focal_writes_require_a_host_camera_lease_even_without_a_remaining_track()
    {
        NativeBattleCameraState host = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraPose original = host.CurrentPose!;
        NativeBattleCameraPose moved = original with { Eye = original.Eye with { X = 1 } };
        Assert.Throws<NotSupportedException>(() => BattleSession.ValidateEffectPresentationCamera(host with { CurrentPose = moved }, host));
        Assert.Throws<NotSupportedException>(() => BattleSession.ValidateEffectPresentationCamera(host with { FocalLength = 512 }, host));
        Assert.Throws<NotSupportedException>(() => BattleSession.ValidateEffectPresentationCamera(host with { Projection = host.Projection! with { FocalLength = 512 } }, host));
    }
}
