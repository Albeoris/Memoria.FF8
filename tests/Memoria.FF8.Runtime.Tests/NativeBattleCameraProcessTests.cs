using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraProcessTests
{
    [Fact]
    public void Authenticated_cold_pose_and_GTE_state_are_distinct_from_500400s_projection_H_write()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        Assert.Equal(new NativeBattleCameraPoint(10000, -3000, 5000), cold.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(0, -500, 0), cold.CurrentPose.Target);
        Assert.All(cold.Projection!.View!.Rotation, value => Assert.Equal((short)0, value));
        Assert.Equal((ushort)0, cold.Projection.FocalLength);
        Assert.Equal((short)0, cold.Projection.OffsetX);
        Assert.Equal((short)0, cold.Projection.OffsetY);
        Assert.True(cold.ProjectionOffsetsCleared);
        NativeBattleCameraState initialized = NativeBattleCameraState.Initialize5041E0(cold).Initialize500400();
        Assert.Equal((ushort)216, initialized.Projection!.FocalLength);
        Assert.Equal(cold.Projection.View, initialized.Projection.View);
        Assert.Equal((short)0, initialized.Projection.OffsetX);
        Assert.Equal((short)0, initialized.FocalLength);
        NativeBattleCameraState priorToDraw = initialized.PublishProjection(0);
        Assert.Equal((ushort)216, priorToDraw.Projection!.FocalLength);
        Assert.Equal((short)0, priorToDraw.Projection.OffsetX);
        NativeBattleCameraState afterDraw = priorToDraw.PublishProjection(4);
        Assert.Equal((ushort)0, afterDraw.Projection!.FocalLength);
        Assert.Equal((short)160, afterDraw.Projection.OffsetX);
        Assert.Equal((short)108, afterDraw.Projection.OffsetY);
    }

    [Fact]
    public void Battle_task_reset_keeps_prior_view_and_offsets_but_resets_roll_and_control_globals()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraState prior = cold with { Mask = 0x8000, Variables = [1, 2, 3, 4, 5, 6, 7, 8], ForceCamera = true, Roll = 123, FocalLength = 777, CurrentPose = cold.CurrentPose! with { Roll = 123, FocalLength = 777 }, Projection = cold.Projection! with { FocalLength = 333, OffsetX = -20, OffsetY = 400 } };
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(prior))!;
        NativeBattleCameraState reset = NativeBattleCameraState.Initialize5041E0(restored).Initialize500400();
        Assert.Equal((ushort)0, reset.Mask);
        Assert.Empty(reset.Order);
        Assert.All(reset.Tasks, Assert.Null);
        Assert.All(reset.Variables, value => Assert.Equal((short)0, value));
        Assert.False(reset.ForceCamera);
        Assert.Equal((short)0, reset.Roll);
        Assert.Equal((short)0, reset.CurrentPose!.Roll);
        Assert.Equal((short)777, reset.FocalLength);
        Assert.Equal(prior.CurrentPose!.Eye, reset.CurrentPose.Eye);
        Assert.Equal(prior.CurrentPose.Target, reset.CurrentPose.Target);
        Assert.Equal((ushort)216, reset.Projection!.FocalLength);
        Assert.Equal((short)-20, reset.Projection.OffsetX);
        Assert.Equal((short)400, reset.Projection.OffsetY);
        Assert.Equal(prior.Projection!.View!.Rotation, reset.Projection.View!.Rotation);
    }

    [Fact]
    public void Unknown_old_projection_does_not_acquire_cold_offsets_or_a_fabricated_view()
    {
        NativeBattleCameraState unknown = NativeBattleCameraState.Initialize5041E0(null).Initialize500400();
        Assert.Null(unknown.CurrentPose);
        Assert.Null(unknown.Projection!.View);
        Assert.Equal((ushort)216, unknown.Projection.FocalLength);
        Assert.Null(unknown.Projection.OffsetX);
        Assert.Null(unknown.Projection.OffsetY);
        Assert.False(unknown.ProjectionOffsetsCleared);
        Assert.Equal((ushort)216, unknown.PublishProjection(0).Projection!.FocalLength);
        Assert.Throws<NotSupportedException>(() => unknown.Projection.ReadPan(new(0, 0, 0)));
        NativeBattleCameraState firstPublished = unknown with { CurrentPose = NativeBattleCameraState.FromColdProcess().CurrentPose, FocalLength = 300 };
        firstPublished = firstPublished.PublishProjection(4);
        Assert.NotNull(firstPublished.Projection!.View);
        Assert.Null(firstPublished.Projection.OffsetX);
        Assert.Null(firstPublished.Projection.OffsetY);
        Assert.Equal((short)160, firstPublished.PublishProjection(4).Projection!.OffsetX);
        NativeBattleCameraState unknownSource = firstPublished with { FocalLength = null };
        Assert.Null(unknownSource.PublishProjection(4).Projection!.FocalLength);
    }
}
