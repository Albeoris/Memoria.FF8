using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraPaddingTests
{
    [Fact]
    public void ColdPaddingIsKnownWhileUnknownOldStateAndResetRemainUnknown()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        Assert.Equal((short?)0, cold.EyePaddingF6);
        Assert.Equal((short?)0, cold.TargetPaddingFE);
        Assert.Equal(new short?[] { 0, 0 }, cold.TrackEyePadding.ToArray());
        NativeBattleCameraState unknown = NativeBattleCameraState.Initialize5041E0(NativeBattleCameraState.Initialize());
        Assert.Null(unknown.EyePaddingF6);
        Assert.Null(unknown.CapturedTargetPadding80E);
        Assert.Equal(new short?[] { null, null }, unknown.TrackTargetPadding.ToArray());
    }

    [Fact]
    public void WordWritersPreservePaddingAndFullVectorCaptureAndPublicationCopyTheirActualOwner()
    {
        NativeBattleCameraState state = NativeBattleCameraState.FromColdProcess() with { EyePaddingF6 = 11, TargetPaddingFE = 12, TrackEyePadding = [21, 22], TrackTargetPadding = [31, 32] };
        NativeBattleCameraPose pose = new(new(1, 2, 3), new(4, 5, 6), 288, 7);
        state = state.WriteEffectGlobals(pose).Capture503300();
        Assert.Equal((short?)11, state.CapturedEyePadding806);
        Assert.Equal((short?)12, state.CapturedTargetPadding80E);
        NativeBattleCameraTrackPose track = new(0, [], [], 288, 288, 0, 0, new(10, 20, 30), new(40, 50, 60));
        state = state with { SelectedSlot = 1, Mask = 1, BlendProgress = 0, RetainedPoses = [null, track] };
        state = state.Tick(0);
        Assert.Equal((short?)22, state.EyePaddingF6);
        Assert.Equal((short?)32, state.TargetPaddingFE);
        NativeBattleCameraState interpolated = (state with { BlendProgress = 2048 }).Tick(0);
        Assert.Equal((short?)22, interpolated.EyePaddingF6);
        Assert.Equal((short?)32, interpolated.TargetPaddingFE);
        state = (state with { BlendProgress = 4096 }).Tick(0);
        Assert.Equal((short?)11, state.EyePaddingF6);
        Assert.Equal((short?)12, state.TargetPaddingFE);
        NativeBattleCameraState reset = NativeBattleCameraState.Initialize5041E0(JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(state))!);
        Assert.Equal(state.TrackEyePadding.ToArray(), reset.TrackEyePadding.ToArray());
        Assert.Equal(state.TargetPaddingFE, reset.TargetPaddingFE);
        Assert.Equal(state.CapturedEyePadding806, reset.CapturedEyePadding806);
    }
}
