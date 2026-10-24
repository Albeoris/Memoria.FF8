using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect64Controller;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect64LowerFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect64ScriptTests
{
    [Fact]
    public void Motion_keeps_positive_Y_and_angular_velocity_uses_separate_fields()
    {
        NativeBattleEffect64Frame frame = Frame(0x1000, 10, 20, 30, 0x1002, 2, 3, 4, 0x1012, 7, 8, 9, 0x1004, 0x1014, 0x10FF);
        NativeBattleEffect64Step result = NativeBattleEffect64Script.Advance6E5DD0(frame, TaskAddress, Resources(), Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(new byte[] { 12, 0, 23, 0, 34, 0 }, ReadBytes(result.Frame, Row, 6).Read(0, 6));
        Assert.Equal(new byte[] { 7, 0, 8, 0, 9, 0 }, ReadBytes(result.Frame, Row + 8, 6).Read(0, 6));
        Assert.Equal(Script + 30, Read(result.Frame, TaskAddress).ReadDword(0x18));
    }

    [Fact]
    public void Loop_counter_is_current_mutable_source_and_survives_JSON()
    {
        NativeBattleEffect64Frame frame = Frame(0x10F0, 1, 3, 4, 0x10FF);
        NativeBattleEffect64Step first = NativeBattleEffect64Script.Advance6E5DD0(frame, TaskAddress, Resources(), Inputs());
        Assert.Equal(0, first.Frame.State.ReadWord(Script + 2));
        NativeBattleEffect64Frame restored = JsonSerializer.Deserialize<NativeBattleEffect64Frame>(JsonSerializer.Serialize(first.Frame))!;
        restored = Write(restored, TaskAddress, Read(restored, TaskAddress).WriteDword(0x18, Script));
        NativeBattleEffect64Step second = NativeBattleEffect64Script.Advance6E5DD0(restored, TaskAddress, Resources(), Inputs());
        Assert.Equal(3, second.Frame.State.ReadWord(Script + 2));
        Assert.Equal(Script + 10, Read(second.Frame, TaskAddress).ReadDword(0x18));
    }

    [Fact]
    public void Row_copy_preserves_unknown_padding_and_termination_does_not_publish_cursor()
    {
        NativeBattleEffect64Frame frame = Frame(0x1080, 1, 0x1090, 0x1100);
        frame = WriteBytes(frame, Row + 16, NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(0, new byte[] { 9, 8 }), 16);
        NativeBattleEffect64Step result = NativeBattleEffect64Script.Advance6E5DD0(frame, TaskAddress, Resources(), Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(Script, Read(result.Frame, TaskAddress).ReadDword(0x18));
        Assert.Equal(new byte[] { 9, 8 }, ReadBytes(result.Frame, Row, 2).Read(0, 2));
        Assert.Equal(128, BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(result.Frame, Row + 6, 2).Read(0, 2)));
        Assert.DoesNotContain(true, ReadBytes(result.Frame, Row + 8, 8).ReadSnapshot(0, 8).Known);
    }
}
