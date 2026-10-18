using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraRawControlTests
{
    [Theory]
    [InlineData(0, 0x80)]
    [InlineData(1, 0x81)]
    [InlineData(0x7F, 0xFF)]
    [InlineData(0x80, 0x80)]
    [InlineData(0xFF, 0xFF)]
    public void Entry_OR_preserves_every_other_control_bit_and_clears_full_mode_WORD_only(byte prior, byte expected)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { Control05 = prior, Mode = 3, ModeHigh19 = 0xCB, ForceCamera = false, Mask = 0xA501, ControlCursor = 19, CurrentPose = new(new(1, 2, 3), new(4, 5, 6), 77, 99), FocalLength = 512, Variables = [1, 2, 3, 4, 5, 6, 7, 8] };
        NativeBattleCameraState actual = camera.Apply50A730();
        NativeBattleCameraState expectedState = camera with { Control05 = expected, Mode = 0, ModeHigh19 = 0 };
        Assert.Equal(JsonSerializer.Serialize(expectedState), JsonSerializer.Serialize(actual));
        Assert.False(actual.ForceCamera);
        Assert.Equal((ushort)0xA501, actual.Mask);
        Assert.Equal(camera.Projection, actual.Projection);
        Assert.Equal(camera.CurrentPose, actual.CurrentPose);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(actual.Apply50A730()));
    }

    [Fact]
    public void Cold_raw_bytes_are_explicit_and_warm_reset_preserves_control05_while_writing_high_mode_zero()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        Assert.Equal((byte)0, cold.Control05);
        Assert.Equal((byte)0, cold.ModeHigh19);
        NativeBattleCameraState warm = cold with { Control05 = 0x35, ModeHigh19 = 0xAB };
        NativeBattleCameraState reset = NativeBattleCameraState.Initialize5041E0(warm);
        Assert.Equal((byte)0x35, reset.Control05);
        Assert.Equal((byte)0, reset.ModeHigh19);
        Assert.Equal((byte)0xB5, reset.Apply50A730().Control05);
        Assert.Null(NativeBattleCameraState.Initialize5041E0(null).Control05);
    }

    [Fact]
    public void Missing_warm_OR_operand_fails_and_full_mode_store_does_not_require_prior_overwritten_bits()
    {
        NativeBattleCameraState missing = NativeBattleCameraState.Initialize();
        Assert.Null(missing.Control05);
        Assert.Throws<NotSupportedException>(() => missing.Apply50A730());
        NativeBattleCameraState overwrite = missing with { Control05 = 7, Mode = null, ModeHigh19 = null };
        Assert.Equal((byte)0, overwrite.Apply50A730().Mode);
        Assert.Equal((byte)0, overwrite.Apply50A730().ModeHigh19);
    }

    [Fact]
    public void Raw_owner_JSON_roundtrip_preserves_next_entry_operation_and_legacy_absence_stays_unknown()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { Control05 = 0x57, ModeHigh19 = 0x92, Mode = 2 };
        NativeBattleCameraState cold = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(camera))!;
        Assert.Equal(JsonSerializer.Serialize(camera.Apply50A730()), JsonSerializer.Serialize(cold.Apply50A730()));
        string legacy = JsonSerializer.Serialize(NativeBattleCameraState.Initialize());
        Assert.DoesNotContain("Control05", legacy);
        Assert.DoesNotContain("ModeHigh19", legacy);
        NativeBattleCameraState unknown = JsonSerializer.Deserialize<NativeBattleCameraState>(legacy)!;
        Assert.Throws<NotSupportedException>(() => unknown.Apply50A730());
    }
}
