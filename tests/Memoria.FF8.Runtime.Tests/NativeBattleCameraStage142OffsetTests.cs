using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraStage142OffsetTests
{
    [Theory]
    [InlineData(-1, -2, 159, 106)]
    [InlineData(32767, 32767, -32609, -32661)]
    [InlineData(-32768, -32768, -32608, -32660)]
    [InlineData(-161, -109, -1, -1)]
    public void Flag_four_publishes_signed_scratch_plus_screen_origin_with_WORD_wrap_then_clears(int x, int y, int expectedX, int expectedY)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { ScreenOffsetScratchX = checked((short)x), ScreenOffsetScratchY = checked((short)y), FocalLength = 512 };
        NativeBattleCameraState first = camera.PublishProjection(4);
        Assert.Equal((short)expectedX, first.Projection!.OffsetX);
        Assert.Equal((short)expectedY, first.Projection.OffsetY);
        Assert.Equal((ushort)512, first.Projection.FocalLength);
        Assert.Equal((short)0, first.ScreenOffsetScratchX);
        Assert.Equal((short)0, first.ScreenOffsetScratchY);
        NativeBattleCameraState second = first.PublishProjection(4);
        Assert.Equal((short)160, second.Projection!.OffsetX);
        Assert.Equal((short)108, second.Projection.OffsetY);
    }

    [Fact]
    public void Without_flag_four_projection_offsets_and_pending_scratch_are_retained()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraState camera = cold with { ScreenOffsetScratchX = -123, ScreenOffsetScratchY = 456, Projection = cold.Projection! with { OffsetX = 41, OffsetY = -51 } };
        NativeBattleCameraState result = camera.PublishProjection(0);
        Assert.Equal((short)41, result.Projection!.OffsetX);
        Assert.Equal((short)-51, result.Projection.OffsetY);
        Assert.Equal((short)-123, result.ScreenOffsetScratchX);
        Assert.Equal((short)456, result.ScreenOffsetScratchY);
        NativeBattleCameraState published = result.PublishProjection(4);
        Assert.Equal((short)37, published.Projection!.OffsetX);
        Assert.Equal((short)564, published.Projection.OffsetY);
    }

    [Fact]
    public void Initialize5041_retains_explicit_scratch_across_snapshot_until_projection_publication()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { ScreenOffsetScratchX = -2, ScreenOffsetScratchY = 1 };
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(camera))!;
        NativeBattleCameraState initialized = NativeBattleCameraState.Initialize5041E0(restored);
        Assert.Equal((short)-2, initialized.ScreenOffsetScratchX);
        Assert.Equal((short)1, initialized.ScreenOffsetScratchY);
        NativeBattleCameraState published = initialized.PublishProjection(4);
        Assert.Equal((short)158, published.Projection!.OffsetX);
        Assert.Equal((short)109, published.Projection.OffsetY);
        Assert.Equal((short)0, published.ScreenOffsetScratchX);
        Assert.Equal((short)0, published.ScreenOffsetScratchY);
    }

    [Fact]
    public void Cold_scratch_is_known_zero_while_initializer_without_retained_owner_keeps_it_unknown()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        Assert.Equal((short)0, cold.ScreenOffsetScratchX);
        Assert.Equal((short)0, cold.ScreenOffsetScratchY);
        NativeBattleCameraState unknown = NativeBattleCameraState.Initialize5041E0(null);
        Assert.Null(unknown.ScreenOffsetScratchX);
        Assert.Null(unknown.ScreenOffsetScratchY);
    }
}
