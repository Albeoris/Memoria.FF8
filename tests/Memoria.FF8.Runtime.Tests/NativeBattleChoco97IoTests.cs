using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco97IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;
    private const uint Base = 0x19DB018;

    [Fact]
    public void Initialization_keeps_current_script_and_exact_native_directories_without_rebasing_external_banks()
    {
        ImmutableArray<byte> script = Script(3, 445, 1, 20);
        NativeBattleChoco97IoInitialization initialized = NativeBattleChoco97Io.Initialize729EF0(script, Buffer, Banks());
        Assert.Equal(Buffer + 0xA000, initialized.NextAddress);
        Assert.Equal(script.ToArray(), initialized.State.Script.ToArray());
        Assert.Equal(0x19DB018u, Dword(initialized.State.Control, 0x104));
        Assert.Equal(0x19DFEF8u, Dword(initialized.State.Control, 0x108));
        Assert.Equal(0x19E4958u, Dword(initialized.State.Control, 0x12C));
        Assert.Equal(0x19E8660u, Dword(initialized.State.Control, 0x130));
        Assert.Throws<NotSupportedException>(() => initialized.State.Banks.Read(Base, 1));
        NativeBattleChoco97IoAdvance waiting = NativeBattleChoco97Io.Advance729FF0(Task().WriteWord(0x24, 0xFFFF), initialized.State);
        Assert.Equal((ushort)0, waiting.Task.ReadWord(0x24));
        Assert.Null(waiting.Request);
        Assert.Equal(NativeBattleChoco97IoState.ScriptAddress, waiting.State.CursorAddress);
        NativeBattleChoco97IoRelease release = NativeBattleChoco97Io.ReleaseWait730590(waiting.State);
        Assert.True(release.Released);
        Assert.False(NativeBattleChoco97Io.ReleaseWait730590(release.State).Released);
        Assert.Equal(NativeBattleChoco97IoState.ScriptAddress + 16, release.State.CursorAddress);
    }

    [Theory]
    [InlineData(2, 1, 0x19DFEF8u, 0x72A250u)]
    [InlineData(3, 1, 0x19DB018u, 0x72A260u)]
    [InlineData(3, 2, 0x19E4958u, 0x72A260u)]
    [InlineData(3, 3, 0x19E8660u, 0x72A260u)]
    public void External_file_begin_publishes_ranges_immediately_and_completion_does_not_upload_TIM(ushort opcode, ushort index, uint destination, uint callback)
    {
        NativeBattleChoco97IoState state = NativeBattleChoco97Io.ReleaseWait730590(NativeBattleChoco97Io.Initialize729EF0(Script(opcode, 445, index, 20), Buffer, Banks()).State).State;
        NativeBattleChoco97IoAdvance begin = NativeBattleChoco97Io.Advance729FF0(Task(), state);
        NativeBattleChoco97IoRequest request = Assert.IsType<NativeBattleChoco97IoRequest>(begin.Request);
        Assert.Equal(opcode, request.Opcode);
        Assert.Equal(index, request.Index);
        Assert.Equal(destination, request.DestinationAddress);
        Assert.Equal(callback, request.CompletionCallback);
        Assert.Equal((short)1, begin.State.RangeCount);
        Assert.Equal(destination, Dword(begin.State.Control, 0));
        Assert.Equal(destination + 20, Dword(begin.State.Control, 0x80));
        Assert.Equal((ushort)1, begin.State.Busy);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(97);
        NativeBattleEffectFileAsset file = new(445, "declared", Enumerable.Repeat((byte)0xA5, 20).ToImmutableArray());
        NativeBattleChoco97IoQueued queued = NativeBattleChoco97Io.CopyAndQueueFile(begin.State, memory, NativeBattleFileIoState.FromColdProcess(), file);
        Assert.Equal(file.Data.ToArray(), queued.State.Banks.Read(destination, 20));
        Assert.Equal((short)1, queued.FileIo.ActiveCount);
        Assert.Equal(0, queued.Memory.Textures!.Count);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco97Io.Complete(queued.State, queued.Memory, 0x72A1F0));
        NativeBattleChoco97IoCompletion completed = NativeBattleChoco97Io.Complete(queued.State, queued.Memory, callback);
        Assert.Equal((ushort)0, completed.State.Busy);
        Assert.Equal(0, completed.Memory.Textures!.Count);
        Assert.Equal(begin.State.CursorAddress, completed.State.CursorAddress);
        NativeBattleChoco97IoAdvance poll = NativeBattleChoco97Io.Advance729FF0(begin.Task, completed.State);
        Assert.Equal((byte)0, poll.Task.ReadByte(0x29));
        Assert.Equal(begin.State.CursorAddress + 16, poll.State.CursorAddress);
    }

    [Fact]
    public void Unsigned_WORD_operands_ignore_padding_but_out_of_owner_directory_index_is_not_signed_minus_one()
    {
        ImmutableArray<byte> script = Script(1, 0xF123, 0xE234, 16);
        byte[] bytes = script.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), 0xBEEF);
        NativeBattleChoco97IoState state = NativeBattleChoco97Io.ReleaseWait730590(NativeBattleChoco97Io.Initialize729EF0(bytes.ToImmutableArray(), Buffer, Banks()).State).State;
        NativeBattleChoco97IoRequest request = NativeBattleChoco97Io.Advance729FF0(Task(), state).Request!;
        Assert.Equal((ushort)0xF123, request.FileId);
        Assert.Equal((ushort)0xE234, request.Index);
        Assert.Equal(Buffer, request.DestinationAddress);
        state = NativeBattleChoco97Io.ReleaseWait730590(NativeBattleChoco97Io.Initialize729EF0(Script(2, 448, 0xFFFF, 16), Buffer, Banks()).State).State;
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97Io.Advance729FF0(Task(), state));
        byte[] moogle = new byte[0x60];
        BinaryPrimitives.WriteUInt16LittleEndian(moogle, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(moogle.AsSpan(2), 438);
        BinaryPrimitives.WriteUInt16LittleEndian(moogle.AsSpan(6), 1);
        BinaryPrimitives.WriteInt32LittleEndian(moogle.AsSpan(8), 16);
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96Io.Advance732390(new(1, 0, null, 0x732390, new byte[0x30 - 12].ToImmutableArray()), NativeBattleMoogle96Io.Initialize7322A0(moogle.ToImmutableArray(), Buffer).State));
    }

    [Fact]
    public void Explicit_caller_declared_overlap_preserves_unwritten_suffix_and_pending_JSON_next_is_independent()
    {
        NativeBattleChoco97IoBanks banks = Banks().Write(0x19E4958, Enumerable.Repeat((byte)0xA5, 20192).ToImmutableArray());
        banks = banks.Write(0x19E4958, Enumerable.Repeat((byte)0x5A, 15624).ToImmutableArray());
        Assert.Equal((byte)0x5A, banks.Read(0x19E4958 + 15623, 1)[0]);
        Assert.Equal((byte)0xA5, banks.Read(0x19E4958 + 15624, 1)[0]);
        banks = banks.Write(0x19E8660, Enumerable.Repeat((byte)0x3C, 25632).ToImmutableArray());
        Assert.Equal((byte)0x3C, banks.Read(0x19E4958 + 15624, 1)[0]);
        Assert.Throws<NotSupportedException>(() => banks.Read(Base - 1, 1));
        NativeBattleChoco97IoState state = NativeBattleChoco97Io.ReleaseWait730590(NativeBattleChoco97Io.Initialize729EF0(Script(3, 445, 1, 20), Buffer, banks).State).State;
        NativeBattleChoco97IoAdvance begin = NativeBattleChoco97Io.Advance729FF0(Task(), state);
        NativeBattleChoco97IoState restored = JsonSerializer.Deserialize<NativeBattleChoco97IoState>(JsonSerializer.Serialize(begin.State))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleChoco97Io.Advance729FF0(begin.Task, begin.State)), JsonSerializer.Serialize(NativeBattleChoco97Io.Advance729FF0(begin.Task, restored)));
        byte[] invalid = begin.State.Control.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(invalid.AsSpan(0x168), -1);
        NativeBattleChoco97IoState invalidRange = state with { Control = invalid.ToImmutableArray() };
        invalidRange = invalidRange with { Control = invalidRange.Control.SetItem(0x164, 0) };
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97Io.Advance729FF0(Task(), invalidRange));
    }

    [Fact]
    public void Terminal_phase_counter_wrap_and_caller_child_count_remain_separate()
    {
        NativeBattleChoco97IoState state = NativeBattleChoco97Io.Initialize729EF0(Script(1, 442, 0, 16), Buffer, Banks()).State with { CursorAddress = NativeBattleChoco97IoState.ScriptAddress + 0xF0 };
        NativeBattleChoco97IoAdvance terminal = NativeBattleChoco97Io.Advance729FF0(Task().WriteWord(0x24, 0xFFFF), state);
        Assert.True(terminal.ReadyToComplete);
        Assert.Equal((ushort)0, terminal.Task.ReadWord(0x24));
        Assert.False(NativeBattleChoco97Io.Advance729FF0(Task().WriteByte(0x28, 1), state).ReadyToComplete);
        Assert.Equal((byte)2, NativeBattleChoco97Io.Advance729FF0(terminal.Task, terminal.State).Task.ReadByte(0x29));
        byte[] unsupported = Script(1, 442, 0, 16).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(unsupported, 6);
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97Io.Advance729FF0(Task(), NativeBattleChoco97Io.Initialize729EF0(unsupported.ToImmutableArray(), Buffer, Banks()).State));
    }

    private static NativeBattleChoco97IoBanks Banks() => new(Base, 0x20000, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x20000));
    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x729FF0, new byte[0x30 - 12].ToImmutableArray());
    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static ImmutableArray<byte> Script(ushort opcode, ushort id, ushort index, int length)
    {
        byte[] script = new byte[256];
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(16), opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(18), id);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(20), index);
        BinaryPrimitives.WriteInt32LittleEndian(script.AsSpan(24), length);
        BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(28), 0xAABBCCDD);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(240), 0xFF);
        return script.ToImmutableArray();
    }
}
