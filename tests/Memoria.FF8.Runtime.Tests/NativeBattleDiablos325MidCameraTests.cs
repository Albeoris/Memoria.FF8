using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325MidCameraTests
{
    [Theory]
    [InlineData(123, -7893, 2394, -8195, 3794)]
    [InlineData(124, -7819, 2341, -8163, 3811)]
    [InlineData(152, -5716, 873, -7260, 4292)]
    public void Current_far_Z_packed_endpoints_and_biased_age_round_each_product_before_sum(int counter, short ey, short ez, short ty, short tz)
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        frame = frame with { Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { FarZ51DC = 4000, CenterZ51D4 = -1234 } } };
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096), 123, 160, 108);
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { Projection = projection, FocalLength = 123, Roll = 17, EyePaddingF6 = 7, TargetPaddingFE = 9 };
        string original = JsonSerializer.Serialize(frame);
        NativeBattleDiablos325TailStep step = NativeBattleDiablos325MidCamera.Advance656429(frame, camera, (ushort)counter);
        Assert.Same(frame, step.Frame);
        Assert.Equal(camera.WriteEffectGlobals(new(new(0, ey, ez), new(0, ty, tz), 123, 17)), step.Camera);
        Assert.Same(projection, step.Camera.Projection);
        Assert.Equal(original, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void Direct_reset_wraps_far_Z_to_WORD_and_leaves_puff_and_current_projection_state_untouched()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        frame = frame with { Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { FarZ51DC = 40000, CenterZ51D4 = -1234, Mode51F0 = 3 } } };
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096), 123, 160, 108);
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { Projection = projection, FocalLength = 123, Roll = 17, EyePaddingF6 = 7, TargetPaddingFE = 9 };
        string original = JsonSerializer.Serialize(frame);
        NativeBattleDiablos325TailStep step = NativeBattleDiablos325MidCamera.Reset6564BC(frame, camera);
        Assert.Same(frame, step.Frame);
        Assert.Equal(camera.WriteEffectGlobals(new(new(0, -6811, -26730), new(0, -6862, -25319), 123, 17)), step.Camera);
        Assert.Same(projection, step.Camera.Projection);
        Assert.Equal(original, JsonSerializer.Serialize(frame));
    }

}
