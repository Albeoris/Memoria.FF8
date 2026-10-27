using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco100IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;
    private static NativeBattleChoco97IoBanks Banks => new(0x19D5018, 0x22300, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x22300));

    [Fact]
    public void Opcode5_selects_114_directory_and_preserves_pending_owner_through_actual_callback()
    {
        NativeBattleChoco100IoInitialization initialized = NativeBattleChoco100Io.Initialize70EE30(Script(0x160, 5, 1, 8), Buffer, Banks);
        Assert.Equal(Buffer + 0xA000, initialized.NextAddress);
        NativeBattleModulePoolTask task = Task(0x70EF30);
        NativeBattleChoco100IoAdvance begun = NativeBattleChoco100Io.Advance70EF30(task, initialized.State);
        Assert.Equal(0x19E8678u, begun.Request!.DestinationAddress);
        Assert.Equal(0x70F1C0u, begun.Request.CompletionCallback);
        Assert.Equal(8, begun.Request.DeclaredLength);
        Assert.Equal((short)1, begun.State.RangeCount);
        Assert.Equal(0x19E8678u, U32(begun.State.Control, 0));
        Assert.Equal(0x19E8680u, U32(begun.State.Control, 0x80));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(100);
        NativeBattleChoco100IoQueued queued = NativeBattleChoco100Io.CopyAndQueueFile(begun.State, memory, NativeBattleFileIoState.FromColdProcess(), new(498, "input", [1, 2, 3, 4]));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, queued.State.Banks.Read(0x19E8678, 4));
        Assert.Throws<NotSupportedException>(() => queued.State.Banks.Read(0x19E867C, 1));
        Assert.Equal((int?)4, queued.FileIo.Rows[0].FileResult);
        Assert.Equal(NativeBattleTestSnapshots.Json(memory.Arena), NativeBattleTestSnapshots.Json(queued.Memory.Arena));
        NativeBattleChoco100IoState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleChoco100IoState>(NativeBattleTestSnapshots.Json(queued.State))!;
        NativeBattleModulePoolTask restoredTask = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(begun.Task));
        NativeBattleChoco100IoAdvance wait = NativeBattleChoco100Io.Advance70EF30(begun.Task, queued.State);
        Assert.Equal(NativeBattleTestSnapshots.Json(wait), NativeBattleTestSnapshots.Json(NativeBattleChoco100Io.Advance70EF30(restoredTask, restored)));
        Assert.Equal(queued.State.CursorAddress, wait.State.CursorAddress);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco100Io.Complete(queued.State, queued.Memory, 0x70F1A0));
        NativeBattleChoco100IoCompletion complete = NativeBattleChoco100Io.Complete(queued.State, queued.Memory, 0x70F1C0);
        Assert.Equal((ushort)0, complete.State.Busy);
        Assert.Equal(NativeBattleTestSnapshots.Json(queued.Memory.Textures), NativeBattleTestSnapshots.Json(complete.Memory.Textures));
        Assert.Equal(queued.State.CursorAddress, complete.State.CursorAddress);
        NativeBattleChoco100IoAdvance poll = NativeBattleChoco100Io.Advance70EF30(begun.Task, complete.State);
        Assert.Equal(complete.State.CursorAddress + 16, poll.State.CursorAddress);
        Assert.Equal((byte)0, poll.Task.ReadByte(0x29));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void Unreached_100_stream_opcodes_remain_unsupported(int opcode)
    {
        NativeBattleChoco100IoState state = NativeBattleChoco100Io.Initialize70EE30(Script(0x160, (ushort)opcode, 0), Buffer, Banks).State;
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100Io.Advance70EF30(Task(0x70EF30), state));
    }

    [Fact]
    public void Earlier_profiles_do_not_inherit_opcode5_and_control_bounds_do_not_invent_a_range_capacity()
    {
        NativeBattleChoco97IoState earlier97 = NativeBattleChoco97Io.Initialize729EF0(Script(0x100, 5, 0), Buffer, Banks).State;
        NativeBattleChoco98IoState earlier98 = NativeBattleChoco98Io.Initialize721CF0(Script(0x120, 5, 0), Buffer, Banks).State;
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97Io.Advance729FF0(Task(0x729FF0), earlier97));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98Io.Advance721DF0(Task(0x721DF0), earlier98));
        NativeBattleChoco99IoState earlier99 = NativeBattleChoco99Io.Initialize718200(Script(0x1A0, 5, 0), Buffer, Banks).State;
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99Io.Advance718310(Task(0x718310), earlier99));
        NativeBattleChoco100IoState overflow = NativeBattleChoco100Io.Initialize70EE30(Script(0x160, 5, ushort.MaxValue), Buffer, Banks).State;
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100Io.Advance70EF30(Task(0x70EF30), overflow));
        NativeBattleChoco100IoState state = NativeBattleChoco100Io.Initialize70EE30(Script(0x160, 5, 0), Buffer, Banks).State;
        byte[] control = state.Control.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(control.AsSpan(0x168), -1);
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100Io.Advance70EF30(Task(0x70EF30), state with { Control = control.ToImmutableArray() }));
    }

    private static ImmutableArray<byte> Script(int length, ushort opcode, ushort selector, int declaredLength = 4)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 498);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), selector);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), declaredLength);
        return bytes.ToImmutableArray();
    }
    private static NativeBattleModulePoolTask Task(uint callback) => new(1, 0, null, callback, new byte[0x30 - 12].ToImmutableArray());
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
