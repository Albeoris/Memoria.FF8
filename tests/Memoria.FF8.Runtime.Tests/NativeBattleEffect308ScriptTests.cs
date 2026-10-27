using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect308Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect308ScriptTests
{
    [Fact]
    public void WORD_motion_wraps_and_positive_Y_bounces_before_yield()
    {
        NativeBattleEffect308Frame frame = Script([1, 0x1000, 32767, 0, 0, 0x1002, 1, 8, 0, 0x1004, 0x10FF], out uint task, out uint row);
        NativeBattleEffect308Step step = AdvanceScript667990(frame, task, null!, null!);
        Assert.Equal((short)-32768, Word308(ReadBytes(step.Frame, row, 2), 0));
        Assert.Equal((short)0, Word308(ReadBytes(step.Frame, row + 2, 2), 0));
        Assert.Equal((short)-6, Word308(ReadBytes(step.Frame, task + 0x1E, 2), 0));
        Assert.Equal(ScriptRanges[0].Address + 22, Read(step.Frame, task).ReadDword(24));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Mutable_loop_counter_and_cursor_continue_after_JSON_then_reset_from_current_operand()
    {
        NativeBattleEffect308Frame frame = Script([1, 0x1001, 1, 0, 0, 0x10FF, 0x10F0, 1, 3, unchecked((ushort)-5), 0x1100], out uint task, out uint row);
        frame = AdvanceScript667990(frame, task, null!, null!).Frame;
        frame = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect308Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        frame = AdvanceScript667990(frame, task, null!, null!).Frame;
        Assert.Equal((short)2, Word308(ReadBytes(frame, row, 2), 0));
        Assert.Equal((ushort)0, frame.State.ReadWord(ScriptRanges[0].Address + 14));
        uint cursor = Read(frame, task).ReadDword(24);
        NativeBattleEffect308Step ended = AdvanceScript667990(frame, task, null!, null!);
        Assert.Equal(2, ended.Result);
        Assert.Equal((ushort)3, ended.Frame.State.ReadWord(ScriptRanges[0].Address + 14));
        Assert.Equal(cursor, Read(ended.Frame, task).ReadDword(24));
    }

    [Fact]
    public void Row_copy_transports_unknown_padding_without_reading_it_as_a_number()
    {
        NativeBattleEffect308Frame frame = Script([1, 0x1080, 2, 0x10FF], out uint task, out uint row);
        uint source = NativeBattleEffect308TestFrame.Main + 0x20 + 32;
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, 16).Select(index => (byte)index).ToArray()).MarkUnknown(6, 2).MarkUnknown(14, 2);
        frame = WriteBytes(frame, source, bytes, 16);
        frame = AdvanceScript667990(frame, task, null!, null!).Frame;
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5 }, ReadBytes(frame, row, 6).Read(0, 6));
        Assert.Throws<NotSupportedException>(() => ReadBytes(frame, row + 6, 2).Read(0, 2));
        Assert.Throws<NotSupportedException>(() => ReadBytes(frame, row + 14, 2).Read(0, 2));
    }

    private static NativeBattleEffect308Frame Script(ushort[] words, out uint task, out uint row)
    {
        NativeBattleEffect308Frame frame = NativeBattleEffect308TestFrame.Create(20);
        frame = frame with { State = frame.State.Write(ScriptRanges[0].Address, words.SelectMany(word => BitConverter.GetBytes(word)).ToArray()) };
        uint rows = NativeBattleEffect308TestFrame.Main + 0x20;
        frame = BirthScript667940(frame, 0x1D97494, rows, ScriptRanges[0].Address);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ScriptDescriptor);
        task = pool.BankAddress;
        row = rows + 16;
        return frame;
    }
}
