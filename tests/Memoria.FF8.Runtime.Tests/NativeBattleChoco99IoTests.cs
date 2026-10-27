using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco99IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;
    private static NativeBattleChoco97IoBanks Banks => new(0x19D3018, 0x1F560, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x1F560));

    [Fact]
    public void Opcode4_selects_144_directory_and_preserves_pending_owner_through_actual_callback()
    {
        NativeBattleChoco99IoInitialization initialized = NativeBattleChoco99Io.Initialize718200(Script(0x1A0, 4, 1), Buffer, Banks);
        Assert.Equal(Buffer + 0xA000, initialized.NextAddress);
        NativeBattleModulePoolTask task = Task(0x718310);
        NativeBattleChoco99IoAdvance begun = NativeBattleChoco99Io.Advance718310(task, initialized.State);
        Assert.Equal(0x19DE510u, begun.Request!.DestinationAddress);
        Assert.Equal(0x718590u, begun.Request.CompletionCallback);
        Assert.Equal((short)1, begun.State.RangeCount);
        Assert.Equal(0x19DE510u, U32(begun.State.Control, 0));
        Assert.Equal(0x19DE514u, U32(begun.State.Control, 0x80));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(99);
        NativeBattleChoco99IoQueued queued = NativeBattleChoco99Io.CopyAndQueueFile(begun.State, memory, NativeBattleFileIoState.FromColdProcess(), new(479, "input", [1, 2, 3, 4]));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, queued.State.Banks.Read(0x19DE510, 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(memory.Arena), NativeBattleTestSnapshots.Json(queued.Memory.Arena));
        NativeBattleChoco99IoState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleChoco99IoState>(NativeBattleTestSnapshots.Json(queued.State))!;
        NativeBattleModulePoolTask restoredTask = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(begun.Task));
        NativeBattleChoco99IoAdvance wait = NativeBattleChoco99Io.Advance718310(begun.Task, queued.State);
        Assert.Equal(NativeBattleTestSnapshots.Json(wait), NativeBattleTestSnapshots.Json(NativeBattleChoco99Io.Advance718310(restoredTask, restored)));
        Assert.Equal(queued.State.CursorAddress, wait.State.CursorAddress);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco99Io.Complete(queued.State, queued.Memory, 0x718580));
        NativeBattleChoco99IoCompletion complete = NativeBattleChoco99Io.Complete(queued.State, queued.Memory, 0x718590);
        Assert.Equal((ushort)0, complete.State.Busy);
        Assert.Equal(NativeBattleTestSnapshots.Json(queued.Memory.Textures), NativeBattleTestSnapshots.Json(complete.Memory.Textures));
        Assert.Equal(queued.State.CursorAddress, complete.State.CursorAddress);
        NativeBattleChoco99IoAdvance poll = NativeBattleChoco99Io.Advance718310(begun.Task, complete.State);
        Assert.Equal(complete.State.CursorAddress + 16, poll.State.CursorAddress);
        Assert.Equal((byte)0, poll.Task.ReadByte(0x29));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void Unreached_99_stream_opcodes_remain_unsupported(int opcode)
    {
        NativeBattleChoco99IoState state = NativeBattleChoco99Io.Initialize718200(Script(0x1A0, (ushort)opcode, 0), Buffer, Banks).State;
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99Io.Advance718310(Task(0x718310), state));
    }

    [Fact]
    public void Earlier_profiles_do_not_inherit_opcode4_and_control_bounds_do_not_invent_a_range_capacity()
    {
        NativeBattleChoco97IoState earlier97 = NativeBattleChoco97Io.Initialize729EF0(Script(0x100, 4, 0), Buffer, Banks).State;
        NativeBattleChoco98IoState earlier98 = NativeBattleChoco98Io.Initialize721CF0(Script(0x120, 4, 0), Buffer, Banks).State;
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97Io.Advance729FF0(Task(0x729FF0), earlier97));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98Io.Advance721DF0(Task(0x721DF0), earlier98));
        NativeBattleChoco99IoState overflow = NativeBattleChoco99Io.Initialize718200(Script(0x1A0, 4, ushort.MaxValue), Buffer, Banks).State;
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99Io.Advance718310(Task(0x718310), overflow));
        NativeBattleChoco99IoState state = NativeBattleChoco99Io.Initialize718200(Script(0x1A0, 4, 0), Buffer, Banks).State;
        byte[] control = state.Control.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(control.AsSpan(0x168), -1);
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99Io.Advance718310(Task(0x718310), state with { Control = control.ToImmutableArray() }));
    }

    private static ImmutableArray<byte> Script(int length, ushort opcode, ushort selector)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 479);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), selector);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 4);
        return bytes.ToImmutableArray();
    }
    private static NativeBattleModulePoolTask Task(uint callback) => new(1, 0, null, callback, new byte[0x30 - 12].ToImmutableArray());
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
