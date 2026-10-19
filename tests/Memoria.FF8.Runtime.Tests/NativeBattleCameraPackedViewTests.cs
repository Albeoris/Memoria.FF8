using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraPackedViewTests
{
    [Fact]
    public void Cold_padding_is_known_and_zero_roll_preserves_current_raw_padding()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        Assert.Equal((ushort?)0, cold.CanonicalViewPadding);
        NativeBattleCameraState camera = cold with { CanonicalViewPadding = 0xBEEF };
        camera = camera.PublishProjection(4);
        NativeBattlePackedMatrix32 packed = camera.CapturePackedView1D97778();
        Assert.All(packed.Known, Assert.True);
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(packed.Bytes.AsSpan().Slice(0x12)));
        Assert.Equal(camera.Projection!.View!.X, BinaryPrimitives.ReadInt32LittleEndian(packed.Bytes.AsSpan().Slice(0x14)));
        Assert.Equal((ushort?)0xBEEF, NativeBattleCameraState.Initialize5041E0(camera).CanonicalViewPadding);
    }

    [Fact]
    public void Nonzero_roll_invalidates_only_packed_padding_and_later_zero_roll_does_not_reconstruct_it()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess();
        camera = camera with { CurrentPose = camera.CurrentPose! with { Roll = 1 }, Roll = 1, CanonicalViewPadding = 0xABCD };
        camera = camera.PublishProjection(4);
        Assert.Null(camera.CanonicalViewPadding);
        NativeBattlePackedMatrix32 packed = camera.CapturePackedView1D97778();
        Assert.Equal(new[] { 0x12, 0x13 }, Enumerable.Range(0, 32).Where(index => !packed.Known[index]));
        camera = NativeBattleCameraState.Initialize5041E0(camera).PublishProjection(4);
        Assert.Null(camera.CanonicalViewPadding);
        Assert.False(camera.CapturePackedView1D97778().Known[0x12]);
    }

    [Theory]
    [InlineData((short)0, (short)700)]
    [InlineData((short)700, (short)0)]
    public void Projection_and_padding_follow_current_global_roll_when_pose_scalar_is_stale(short poseRoll, short globalRoll)
    {
        NativeBattleCameraPose pose = new(new(1000, -2000, 4000), new(-200, -300, 100), 300, poseRoll);
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().WriteEffectGlobals(pose) with { Roll = globalRoll, CanonicalViewPadding = 0xBEEF };
        camera = camera.PublishProjection(4);
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.CameraView(pose with { Roll = globalRoll });
        NativeBattleActionMatrix actual = camera.Projection!.View!;
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((expected.X, expected.Y, expected.Z), (actual.X, actual.Y, actual.Z));
        Assert.Equal(globalRoll == 0 ? (ushort?)0xBEEF : null, camera.CanonicalViewPadding);
        Assert.Same(pose, camera.CurrentPose);
    }

    [Fact]
    public void Missing_warm_padding_stays_unknown_through_disk_restore_and_Tonberry_raw_copy()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraState warm = cold with { CanonicalViewPadding = null };
        string json = JsonSerializer.Serialize(warm);
        Assert.DoesNotContain("CanonicalViewPadding", json);
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(json)!;
        Assert.Null(NativeBattleCameraState.Initialize5041E0(restored).CanonicalViewPadding);
        NativeBattlePackedMatrix32 packed = restored.PublishProjection(4).CapturePackedView1D97778();
        var fixture = NativeBattleTonberry90ControllerTests.Fixture();
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Controller.Advance7624D0(fixture.Frame, fixture.Inputs with { CameraMatrix = packed });
        Assert.Equal(packed, frame.Memory.SharedEffectView2793E58);
        Assert.False(frame.Memory.SharedEffectView2793E58!.Known[0x13]);
        Assert.Null(NativeBattleCameraState.Initialize5041E0(null).CanonicalViewPadding);
    }
}
