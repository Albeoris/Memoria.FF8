using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraEffectGlobalsTests
{
    [Fact]
    public void Effect_global_write_preserves_prior_projection_and_all_camera_task_blend_and_control_owners()
    {
        NativeBattleCameraState source = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraBank bank = new([1, 0, 4, 0, 0, 0]);
        source = source with { Mask = 4, Tasks = [new(2, bank, 0, 0, 0), null], Order = [0], SelectedSlot = 0, CapturedPose = source.CurrentPose, BlendProgress = 123, ForceCamera = true, ControlCursor = 4, Variables = source.Variables.SetItem(0, 17), Scratch = source.Scratch.SetItem(2, -7) };
        NativeBattleCameraPose effect = new(new(0, -3827, -1983), new(0, -5150, -723), 288, 0);
        NativeBattleCameraState actual = source.WriteEffectGlobals(effect);
        Assert.Same(effect, actual.CurrentPose);
        Assert.Equal((short)288, actual.FocalLength);
        Assert.Equal((short)0, actual.Roll);
        Assert.Same(source.Projection, actual.Projection);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(actual with { CurrentPose = source.CurrentPose, FocalLength = source.FocalLength, Roll = source.Roll }));
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(actual))!;
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored));
    }

    [Theory]
    [InlineData(0u, 216)]
    [InlineData(4u, 288)]
    public void Normal_publication_always_updates_view_but_updates_GTE_H_only_on_native_publication_bit(uint flags, ushort expectedH)
    {
        NativeBattleCameraState source = NativeBattleCameraState.FromColdProcess().Initialize500400();
        NativeBattleCameraPose effect = new(new(0, -3827, -1983), new(0, -5150, -723), 288, 0);
        NativeBattleCameraState written = source.WriteEffectGlobals(effect);
        Assert.Equal((ushort)216, written.Projection!.FocalLength);
        Assert.Same(source.Projection!.View, written.Projection.View);
        NativeBattleCameraState published = written.PublishProjection(flags);
        NativeBattleActionMatrix expectedView = NativeBattlePoseMatrices.CameraView(effect);
        Assert.Equal(expectedH, published.Projection!.FocalLength);
        Assert.True(expectedView.Rotation.SequenceEqual(published.Projection.View!.Rotation));
        Assert.Equal(expectedView.X, published.Projection.View.X);
        Assert.Equal(expectedView.Y, published.Projection.View.Y);
        Assert.Equal(expectedView.Z, published.Projection.View.Z);
        Assert.Equal(flags == 0 ? (short)0 : (short)160, published.Projection.OffsetX);
        Assert.Equal(flags == 0 ? (short)0 : (short)108, published.Projection.OffsetY);
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(written))!;
        Assert.Equal(JsonSerializer.Serialize(published), JsonSerializer.Serialize(restored.PublishProjection(flags)));
    }
}
