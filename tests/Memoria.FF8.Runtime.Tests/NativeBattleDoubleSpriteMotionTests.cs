using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoubleSpriteMotionTests
{
    [Theory]
    [InlineData(0, 1, 24, 0x80)]
    [InlineData(1, 2, 48, 0x80)]
    [InlineData(2, 3, 64, 0x80)]
    [InlineData(3, 4, 17, 0x80)]
    [InlineData(4, 5, 48, 0x80)]
    [InlineData(5, 6, 24, 0x80)]
    [InlineData(6, 6, 0, 0x81)]
    [InlineData(7, 7, 17, 0x80)]
    public void Eight_phase_sprite_changes_color_before_exactly_two_CRT_draws_even_when_already_complete(int phase, int nextPhase, int red, int flags)
    {
        NativeBattleModulePoolTask task = Task(0x8D58A0, phase).WriteWord(0x50, 0xABFE);
        NativeCrtRandomState random = new(1);
        NativeCrtRandomState expected = random.Draw(out ushort angle).Draw(out ushort frame);
        NativeBattleDoubleSpriteMotionStep step = NativeBattleDoubleSpriteMotion.Advance(task, random);
        Assert.Equal((byte)nextPhase, step.Task.ReadByte(0x29));
        Assert.Equal((byte)red, step.Task.ReadByte(0x48));
        Assert.Equal((byte)flags, step.Task.ReadByte(0x26));
        Assert.Equal((ushort)(angle & 0xFFF), step.Task.ReadWord(0x40));
        Assert.Equal((ushort)((frame % 7 + 255) & 7), step.Task.ReadWord(0x50));
        Assert.Equal(expected, step.Random);
        Assert.Equal(task.ReadWord(0x24), step.Task.ReadWord(0x24));
        Assert.Equal(task.ReadByte(0x28), step.Task.ReadByte(0x28));
        Assert.Equal(task.ReadByte(0x4B), step.Task.ReadByte(0x4B));
    }

    [Theory]
    [InlineData(0, 17, 0, 17)]
    [InlineData(1, 17, 2, 64)]
    [InlineData(2, 36, 0, 32)]
    [InlineData(2, 0, 2, 252)]
    [InlineData(3, 17, 4, 64)]
    [InlineData(4, 36, 0, 32)]
    [InlineData(-1, 17, -1, 17)]
    public void Native_color_switch_preserves_WORD_signed_selector_and_BYTE_wrap(int selector, int red, int nextSelector, int nextRed)
    {
        NativeBattleModulePoolTask task = Task(0x8D6200, 3).WriteWord(0x64, unchecked((ushort)selector)).WriteByte(0x48, (byte)red);
        NativeBattleDoubleSpriteMotionStep step = NativeBattleDoubleSpriteMotion.Advance(task, new(9));
        Assert.Equal(unchecked((ushort)nextSelector), step.Task.ReadWord(0x64));
        Assert.Equal((byte)nextRed, step.Task.ReadByte(0x48));
        Assert.Equal(new NativeCrtRandomState(9), step.Random);
    }

    [Theory]
    [InlineData(0, 1024)]
    [InlineData(1024, 0)]
    [InlineData(2048, -1024)]
    [InlineData(3072, 0)]
    public void Center_copy_uses_current_center_words_before_signed_trigonometric_offset(int angle, int zOffset)
    {
        NativeBattleModulePoolTask task = Task(0x8D6200, 3).WriteWord(0x58, 32760).WriteWord(0x5A, unchecked((ushort)-120)).WriteWord(0x5C, 200).WriteWord(0x5E, 0x55AA).WriteWord(0x62, (ushort)angle);
        NativeBattleDoubleSpriteMotionStep step = NativeBattleDoubleSpriteMotion.Advance(task, new(4));
        Assert.Equal(task.ReadWord(0x5A), step.Task.ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)(200 + zOffset)), step.Task.ReadWord(0x20));
        Assert.Equal((ushort)0x55AA, step.Task.ReadWord(0x22));
        if (angle == 1024)
        {
            Assert.Equal(unchecked((ushort)(32760 + 1024)), step.Task.ReadWord(0x1C));
        }
    }

    [Theory]
    [InlineData(2, 2, 4, 64)]
    [InlineData(3, 1, 0, 17)]
    public void Parent_phase_read_is_lazy_and_precedes_same_callback_color_switch(int firstWord, int nextPhase, int colorState, int color)
    {
        NativeBattleModulePoolTask task = Task(0x8D6200, 1).WriteDword(0x14, 0x1234);
        int reads = 0;
        NativeBattleDoubleSpriteMotionStep step = NativeBattleDoubleSpriteMotion.Advance(task, new(3), pointer =>
        {
            Assert.Equal(0x1234u, pointer);
            reads++;
            return Task(0, 0).WriteWord(0x58, (ushort)firstWord);
        });
        Assert.Equal(1, reads);
        Assert.Equal((byte)nextPhase, step.Task.ReadByte(0x29));
        Assert.Equal((ushort)colorState, step.Task.ReadWord(0x64));
        Assert.Equal((byte)color, step.Task.ReadByte(0x48));
        Assert.Throws<NotSupportedException>(() => NativeBattleDoubleSpriteMotion.Advance(task, new(3)));
    }

    [Theory]
    [InlineData(0, 28, 1, 32, 0x80)]
    [InlineData(0, 254, 0, 2, 0x80)]
    [InlineData(2, 4, 3, 0, 0x85)]
    [InlineData(2, 2, 2, 254, 0x80)]
    public void Native_rise_and_fade_use_unsigned_BYTE_gates_without_saturation(int phase, int color, int nextPhase, int nextColor, int flags)
    {
        NativeBattleModulePoolTask task = Task(0x8D6200, phase).WriteByte(0x48, (byte)color);
        NativeBattleDoubleSpriteMotionStep step = NativeBattleDoubleSpriteMotion.Advance(task, new(8));
        Assert.Equal((byte)nextPhase, step.Task.ReadByte(0x29));
        Assert.Equal((byte)nextColor, step.Task.ReadByte(0x48));
        Assert.Equal((byte)flags, step.Task.ReadByte(0x26));
        Assert.Equal(task.ReadWord(0x24), step.Task.ReadWord(0x24));
    }

    [Fact]
    public void Current_task_JSON_retains_next_motion_and_rejects_unsupported_signed_phase()
    {
        NativeBattleModulePoolTask original = Task(0x8D6200, 2).WriteWord(0x64, 4).WriteByte(0x48, 36);
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(original))!;
        NativeBattleDoubleSpriteMotionStep first = NativeBattleDoubleSpriteMotion.Advance(original, new(20));
        NativeBattleDoubleSpriteMotionStep second = NativeBattleDoubleSpriteMotion.Advance(restored, new(20));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Throws<InvalidDataException>(() => NativeBattleDoubleSpriteMotion.Advance(original.WriteByte(0x29, 255), new(20)));
    }

    private static NativeBattleModulePoolTask Task(uint callback, int phase)
    {
        NativeBattleModulePoolTask task = new(1, 7, 3, callback, ImmutableArray.CreateRange(Enumerable.Repeat((byte)0xA5, 92)));
        return task.WriteByte(0x29, (byte)phase).WriteByte(0x26, 0x80).WriteWord(0x24, 123).WriteByte(0x48, 17).WriteByte(0x49, 18).WriteByte(0x4A, 19).WriteWord(0x64, 0).WriteDword(0x58, 0).WriteDword(0x5C, 0).WriteWord(0x62, 0);
    }
}
