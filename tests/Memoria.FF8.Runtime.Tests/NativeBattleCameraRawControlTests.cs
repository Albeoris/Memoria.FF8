using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraRawControlTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void Entry_sets_existing_forced_camera_bit_and_clears_mask_preserving_mode_and_other_owners(byte mode, bool force)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { Mode = mode, ForceCamera = force, Mask = 0xA501, BlendProgress = 177, ControlCursor = 19, CurrentPose = new(new(1, 2, 3), new(4, 5, 6), 77, 99), FocalLength = 512, Variables = [1, 2, 3, 4, 5, 6, 7, 8] };
        NativeBattleCameraState actual = camera.Apply50A730();
        Assert.Equal(JsonSerializer.Serialize(camera with { ForceCamera = true, Mask = 0 }), JsonSerializer.Serialize(actual));
        Assert.Equal(mode, actual.Mode);
        Assert.Equal((ushort)177, actual.BlendProgress);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(actual.Apply50A730()));
    }

    [Fact]
    public void Entry_overwrites_missing_forced_bit_without_reading_it_and_preserves_unknown_mode()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize() with { Mode = null, ForceCamera = null, Mask = ushort.MaxValue };
        NativeBattleCameraState actual = camera.Apply50A730();
        Assert.True(actual.ForceCamera);
        Assert.Equal((ushort)0, actual.Mask);
        Assert.Null(actual.Mode);
    }

    [Fact]
    public void Existing_owner_JSON_roundtrip_preserves_entry_and_actual_complete_action_consumer()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { Mode = 2, Mask = 0x1234, BlendProgress = 73 };
        NativeBattleCameraState cold = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(camera))!;
        Assert.Equal(JsonSerializer.Serialize(camera.Apply50A730()), JsonSerializer.Serialize(cold.Apply50A730()));
        NativeBattleCameraState completed = cold.Apply50A730().CompleteAction();
        Assert.False(completed.ForceCamera);
        Assert.Equal((ushort)4096, completed.BlendProgress);
        Assert.Equal((byte)2, completed.Mode);
        Assert.Equal((ushort)0, completed.Mask);
        string json = JsonSerializer.Serialize(completed);
        Assert.DoesNotContain("Control05", json);
        Assert.DoesNotContain("ModeHigh19", json);
    }
}
