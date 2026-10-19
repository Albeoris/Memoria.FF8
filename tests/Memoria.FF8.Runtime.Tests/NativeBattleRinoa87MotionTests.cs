using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa87MotionTests
{
    [Fact]
    public void Movement_updates_acceleration_before_progress_and_uses_signed_truncation()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(320, 100).WriteWord(328, 500).WriteWord(362, 400).WriteWord(364, 100).WriteWord(366, unchecked((ushort)-20));
        task = NativeBattleRinoa87Motion.Move7762D0(task);
        Assert.Equal((ushort)500, task.ReadWord(362));
        Assert.Equal((ushort)500, task.ReadWord(360));
        Assert.Equal((ushort)148, task.ReadWord(76));
        Assert.Equal(unchecked((ushort)-20), task.ReadWord(78));
    }

    [Fact]
    public void Overshoot_copies_the_complete_raw_destination_before_adding_arc()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(360, 4000).WriteWord(362, 200).WriteWord(366, unchecked((ushort)-7)).WriteDword(328, 0x0014000A).WriteDword(332, 0xBEEF001E);
        NativeBattleModulePoolTask clone = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
        task = NativeBattleRinoa87Motion.Move7762D0(task);
        Assert.Equal((ushort)4096, task.ReadWord(360));
        Assert.Equal((ushort)10, task.ReadWord(76));
        Assert.Equal((ushort)13, task.ReadWord(78));
        Assert.Equal(0xBEEF001Eu, task.ReadDword(80));
        Assert.Equal(JsonSerializer.Serialize(task), JsonSerializer.Serialize(NativeBattleRinoa87Motion.Move7762D0(clone)));
    }

    [Fact]
    public void Native_WORD_acceleration_wraps_before_the_signed_completion_comparison()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(360, 9).WriteWord(362, 32760).WriteWord(364, 10);
        task = NativeBattleRinoa87Motion.Move7762D0(task);
        Assert.Equal(unchecked((ushort)-32766), task.ReadWord(362));
        Assert.Equal(unchecked((ushort)-32757), task.ReadWord(360));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(16, 0)]
    [InlineData(2048, 2048)]
    [InlineData(4095, 0)]
    public void Yaw_return_preserves_the_original_half_turn_equality_branch(int source, int expected)
    {
        NativeBattleModulePoolTask task = NativeBattleRinoa87Motion.TurnTowardZero77F070(Task().WriteWord(62, (ushort)source));
        Assert.Equal((ushort)expected, task.ReadWord(62));
    }

    [Theory]
    [InlineData(-1, 4096)]
    [InlineData(2047, 4096)]
    [InlineData(2048, 4096)]
    [InlineData(2050, 4097)]
    [InlineData(10240, 8192)]
    [InlineData(32767, 8192)]
    public void Impact_program_scale_uses_signed_target_WORD_and_native_clamp(int targetScale, int expected)
    {
        Assert.Equal((short)expected, NativeBattleRinoa87Program.Scale77E780((short)targetScale));
    }

    [Fact]
    public void Ground_capture_preserves_the_quotient_modulo64_rounding_and_temporary_offsets()
    {
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 256, 160, 108);
        NativeBattleThundaga105CaptureTransform capture = NativeBattleRinoa87GroundCapture.Transform778AB0(new(6, 99, 9), new(new(-64, 0, -64), new(64, 0, 64)), projection, 256);
        Assert.Equal(-2, capture.Matrix.X);
        Assert.Equal(3, capture.Matrix.Y);
        Assert.Equal(256, capture.Matrix.Z);
        Assert.Equal((short)32, capture.Projection.OffsetX);
        Assert.Equal((short)32, capture.Projection.OffsetY);
        Assert.Equal((short)160, capture.PriorOffsetX);
        Assert.Equal((short)108, capture.PriorOffsetY);
        Assert.Equal((short)160, projection.OffsetX);
    }

    private static NativeBattleModulePoolTask Task() => new(0, 0, null, 0x7751A0, new byte[376 - 12].ToImmutableArray());
}
