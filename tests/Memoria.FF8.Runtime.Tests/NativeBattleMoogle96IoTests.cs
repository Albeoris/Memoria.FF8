using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMoogle96IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;

    [Fact]
    public void Initialization_clears_only_control_and_keeps_retained_script_destinations_until_each_file_begins()
    {
        ImmutableArray<byte> script = Script();
        NativeBattleMoogle96IoInitialization initialized = NativeBattleMoogle96Io.Initialize7322A0(script, Buffer);
        Assert.Equal(Buffer + 0xA000, initialized.NextAddress);
        Assert.Equal(script.ToArray(), initialized.State.Script.ToArray());
        byte[] expected = new byte[0x16C];
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(0x100), Buffer);
        for (int offset = 0x104; offset <= 0x160; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(offset), 0x1D9B018);
        }
        Assert.Equal(expected, initialized.State.Control.ToArray());
        NativeBattleMoogle96IoAdvance waiting = NativeBattleMoogle96Io.Advance732390(Task().WriteWord(0x24, 0xFFFF), initialized.State);
        Assert.Null(waiting.Request);
        Assert.False(waiting.ReadyToComplete);
        Assert.Equal((ushort)0, waiting.Task.ReadWord(0x24));
        Assert.Equal(NativeBattleMoogle96IoState.ScriptAddress, waiting.State.CursorAddress);
        NativeBattleMoogle96IoRelease released = NativeBattleMoogle96Io.ReleaseWait733A80(waiting.State);
        Assert.True(released.Released);
        Assert.Equal((ushort)1, released.State.WaitReleased);
        Assert.Equal(NativeBattleMoogle96IoState.ScriptAddress + 16, released.State.CursorAddress);
        Assert.False(NativeBattleMoogle96Io.ReleaseWait733A80(released.State).Released);
        NativeBattleMoogle96IoAdvance begun = NativeBattleMoogle96Io.Advance732390(waiting.Task, released.State);
        Assert.Equal(new NativeBattleMoogle96IoRequest(438, Buffer, 16, 0x732590), begun.Request);
        Assert.Equal((byte)1, begun.Task.ReadByte(0x29));
        Assert.Equal((ushort)1, begun.State.Busy);
        Assert.Equal(Buffer, BinaryPrimitives.ReadUInt32LittleEndian(begun.State.Script.AsSpan().Slice(16 + 12)));
        Assert.Equal(0xAABBCCDDu, BinaryPrimitives.ReadUInt32LittleEndian(script.AsSpan().Slice(16 + 12)));
    }

    [Fact]
    public void File_copy_enforces_file_identity_and_actual_buffer_extent_and_keeps_busy_cursor_task_and_upload_queue_separate()
    {
        NativeBattleMoogle96IoState state = NativeBattleMoogle96Io.ReleaseWait733A80(NativeBattleMoogle96Io.Initialize7322A0(Script(), Buffer).State).State;
        NativeBattleMoogle96IoAdvance begun = NativeBattleMoogle96Io.Advance732390(Task(), state);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(96);
        NativeBattleFileIoState queue = NativeBattleFileIoState.FromColdProcess();
        NativeBattleEffectFileAsset file = new(438, "fixture", Enumerable.Repeat((byte)0xA5, 16).ToImmutableArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleMoogle96Io.CopyAndQueueFile(begun.State, memory, queue, file with { FileId = 439 }));
        Assert.Throws<InvalidDataException>(() => NativeBattleMoogle96Io.CopyAndQueueFile(begun.State, memory, queue, file with { Data = [] }));
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96Io.CopyAndQueueFile(begun.State, memory, queue, file with { Data = new byte[0xA001].ToImmutableArray() }));
        NativeBattleEffectMemory retained = memory with { Arena = memory.Arena!.Write(0x20001, [0xBC]) };
        NativeBattleMoogle96IoQueued shorter = NativeBattleMoogle96Io.CopyAndQueueFile(begun.State, retained, queue, file with { Data = [0] });
        Assert.Equal(new byte[] { 0, 0xBC }, shorter.Memory.Arena!.Read(0x20000, 2));
        Assert.Equal((int?)1, shorter.FileIo.Rows[0].FileResult);
        NativeBattleMoogle96IoQueued copied = NativeBattleMoogle96Io.CopyAndQueueFile(begun.State, memory, queue, file);
        Assert.Equal(file.Data.ToArray(), copied.Memory.Arena!.Read(0x20000, 16));
        Assert.Equal(0, copied.Memory.Textures!.Count);
        Assert.Equal((short)1, copied.FileIo.ActiveCount);
        Assert.Equal(0x732590u, copied.FileIo.Rows[0].CompletionCallback);
        NativeBattleMoogle96IoAdvance poll = NativeBattleMoogle96Io.Advance732390(begun.Task, begun.State);
        Assert.Null(poll.Request);
        Assert.Equal((byte)1, poll.Task.ReadByte(0x29));
        Assert.Equal(begun.State.CursorAddress, poll.State.CursorAddress);
        Assert.Equal((ushort)1, poll.State.Busy);
        NativeBattleMoogle96IoState restored = JsonSerializer.Deserialize<NativeBattleMoogle96IoState>(JsonSerializer.Serialize(begun.State))!;
        Assert.Equal(JsonSerializer.Serialize(poll), JsonSerializer.Serialize(NativeBattleMoogle96Io.Advance732390(begun.Task, restored)));
    }

    [Fact]
    public void Terminal_sets_flag_and_completes_on_that_callback_but_child_release_remains_the_pool_callers_owner()
    {
        NativeBattleMoogle96IoState state = NativeBattleMoogle96Io.Initialize7322A0(Script(), Buffer).State with { CursorAddress = NativeBattleMoogle96IoState.ScriptAddress + 80 };
        NativeBattleMoogle96IoAdvance completed = NativeBattleMoogle96Io.Advance732390(Task(), state);
        Assert.True(completed.ReadyToComplete);
        Assert.Equal((byte)1, completed.Task.ReadByte(0x29));
        Assert.Equal((byte)1, (byte)(completed.Task.ReadByte(0x26) & 1));
        Assert.Equal((ushort)1, completed.Task.ReadWord(0x24));
        Assert.Null(completed.Request);
        Assert.Equal(state.CursorAddress, completed.State.CursorAddress);
        Assert.False(NativeBattleMoogle96Io.Advance732390(Task().WriteByte(0x28, 1), state).ReadyToComplete);
        Assert.Throws<InvalidDataException>(() => NativeBattleMoogle96Io.Initialize7322A0(default, Buffer));
        Assert.Throws<InvalidDataException>(() => NativeBattleMoogle96Io.Initialize7322A0(Script(), 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96Io.Complete732590(state, NativeBattleEffectMemory.FromColdProcess().BeginLoad(96)));
        byte[] unsupported = Script().ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(unsupported, 6);
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96Io.Advance732390(Task(), NativeBattleMoogle96Io.Initialize7322A0(unsupported.ToImmutableArray(), Buffer).State));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x732390, new byte[0x30 - 12].ToImmutableArray());
    private static ImmutableArray<byte> Script()
    {
        byte[] bytes = new byte[0x60];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 438);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 0xAABBCCDD);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(80), 0xFF);
        return bytes.ToImmutableArray();
    }
}
