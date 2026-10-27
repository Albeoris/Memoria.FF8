using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurse47IoTests
{
    private const uint Buffer = NativeBattleEffectNodeMemory.ArenaAddress + 0x20000;

    [Fact]
    public void Initialization_owns_the_actual_control_and_preserves_warm_script_and_unrelated_globals()
    {
        byte[] script = Script().ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(28), 0x12345678);
        NativeBattleCurse47State before = NativeBattleCurse47State.FromColdProcess(script.ToImmutableArray()).WriteDword(NativeBattleCurse47Io.NextAddressGlobal, Buffer).WithSharedWord(8, 0xABCD);
        NativeBattleCurse47State state = NativeBattleCurse47Io.Initialize(before);
        Assert.Equal(Buffer + 0xA000, state.ReadDword(NativeBattleCurse47Io.NextAddressGlobal));
        Assert.Equal(NativeBattleCurse47Io.ScriptAddress, state.ReadDword(NativeBattleCurse47Io.CursorGlobal));
        Assert.Equal(NativeBattleCurse47Io.ControlAddress, state.ReadDword(NativeBattleCurse47Io.ControlPointerGlobal));
        Assert.Equal(script, state.IoScript!.Value.ToArray());
        Assert.Equal((ushort)0xABCD, state.SharedWord(8));
        Assert.All(state.Read(NativeBattleCurse47Io.ControlAddress, 0x100), value => Assert.Equal((byte)0, value));
        for (int offset = 0x104; offset <= 0x160; offset += 4)
        {
            uint expected = offset switch
            {
                >= 0x114 and <= 0x124 => 0x19EB018,
                0x128 => 0x19ECBB8,
                0x12C => 0x19ED83C,
                _ => 0x19F0324
            };
            Assert.Equal(expected, state.ReadDword(NativeBattleCurse47Io.ControlAddress + (uint)offset));
        }
        Assert.All(state.Read(NativeBattleCurse47Io.ControlAddress + 0x164, 8), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void Three_TIM_rows_copy_into_the_owned_buffer_queue_real_completion_then_terminate()
    {
        NativeBattleCurse47State state = NativeBattleCurse47Io.ReleaseWait(Initialized()).State;
        NativeBattleModulePoolTask task = Task();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleFileIoState io = NativeBattleFileIoState.FromColdProcess();
        int uploads = memory.Textures!.Count;
        int[] sizes = [33312, 33312, 32832];
        for (int index = 0; index < 3; index++)
        {
            NativeBattleCurse47IoAdvance begun = NativeBattleCurse47Io.AdvanceTask(task, state);
            NativeBattleCurse47IoRequest request = Assert.IsType<NativeBattleCurse47IoRequest>(begun.Request);
            state = begun.State;
            task = begun.Task;
            Assert.Equal((ushort)1, request.Opcode);
            Assert.Equal((ushort)(388 + index), request.FileId);
            Assert.Equal(sizes[index], request.DeclaredLength);
            Assert.Equal(Buffer, request.DestinationAddress);
            Assert.Equal(0x82AF90u, request.CompletionCallback);
            byte[] payload = Tim(sizes[index], (byte)(0xA0 + index));
            var queued = NativeBattleCurse47Io.CopyAndQueueFile(state, memory, io, new((short)request.FileId, "fixture", payload.ToImmutableArray()));
            memory = queued.Memory;
            io = queued.FileIo;
            Assert.Equal(uploads + index, memory.Textures!.Count);
            Assert.Equal(payload, memory.Arena!.Read(0x20000, sizes[index]));
            NativeBattleCurse47IoAdvance waiting = NativeBattleCurse47Io.AdvanceTask(task, state);
            Assert.Null(waiting.Request);
            Assert.Equal(state.ReadDword(NativeBattleCurse47Io.CursorGlobal), waiting.State.ReadDword(NativeBattleCurse47Io.CursorGlobal));
            int ticks = 0;
            while (io.ActiveCount != 0)
            {
                Assert.True(ticks++ < 8);
                io = io.Advance482590((current, completion) =>
                {
                    Assert.Equal(0x82AF90u, completion.Callback);
                    var finished = NativeBattleCurse47Io.Complete(state, memory, completion.Callback);
                    state = finished.State;
                    memory = finished.Memory;
                    return current;
                });
            }
            Assert.Equal(uploads + index + 1, memory.Textures!.Count);
            NativeBattleCurse47IoAdvance polled = NativeBattleCurse47Io.AdvanceTask(waiting.Task, state);
            Assert.Equal((byte)0, polled.Task.ReadByte(0x29));
            Assert.Equal((ushort)0, polled.State.ReadIoControlWord(0x168));
            task = polled.Task;
            state = polled.State;
        }
        NativeBattleCurse47IoAdvance final = NativeBattleCurse47Io.AdvanceTask(task, state);
        Assert.True(final.ReadyToComplete);
        Assert.Null(final.Request);
        Assert.Equal((ushort)0, final.State.ReadIoControlWord(0x166));
        Assert.Equal((byte)1, final.Task.ReadByte(0x29));
    }

    [Fact]
    public void Pending_cold_state_has_identical_callback_and_next_poll_without_reinitialization()
    {
        NativeBattleCurse47IoAdvance begun = NativeBattleCurse47Io.AdvanceTask(Task(), NativeBattleCurse47Io.ReleaseWait(Initialized()).State);
        NativeBattleCurse47IoAdvance cold = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleCurse47IoAdvance>(SnapshotJson(begun))!);
        NativeBattleCurse47State invalidCallbackState = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(begun.State));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x20000, Tim(33312, 0xAA)) };
        var a = NativeBattleCurse47Io.Complete(begun.State, memory, 0x82AF90);
        var b = NativeBattleCurse47Io.Complete(cold.State, memory, 0x82AF90);
        Assert.Equal(SnapshotJson(a.State), SnapshotJson(b.State));
        Assert.Equal(SnapshotJson(a.Memory), SnapshotJson(b.Memory));
        Assert.Equal(SnapshotJson(NativeBattleCurse47Io.AdvanceTask(begun.Task, a.State)), SnapshotJson(NativeBattleCurse47Io.AdvanceTask(cold.Task, b.State)));
        Assert.Throws<InvalidDataException>(() => NativeBattleCurse47Io.Complete(invalidCallbackState, memory, 0x82B000));
    }

    [Fact]
    public void Explicit_stream_publisher_retains_its_state_and_memory_outputs_and_requires_the_real_ready_byte()
    {
        NativeBattleCurse47State state = NativeBattleCurse47Io.ReleaseWait(Initialized()).State;
        byte[] script = state.IoScript!.Value.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(16), 6);
        BinaryPrimitives.WriteInt32LittleEndian(script.AsSpan(24), 0);
        state = state with { IoScript = script.ToImmutableArray() };
        NativeBattleCurse47IoAdvance begun = NativeBattleCurse47Io.AdvanceTask(Task(), state);
        Assert.Equal(0x82AFC0u, begun.Request!.CompletionCallback);
        var completed = NativeBattleCurse47Io.Complete(begun.State, NativeBattleEffectMemory.FromColdProcess(), 0x82AFC0, (current, memory, address) =>
        {
            Assert.Equal(Buffer, address);
            return (current.WithSharedWord(8, 0xABCD), memory with { SoundPublishedFlags = 0x77 });
        });
        Assert.Equal((ushort)0xABCD, completed.State.SharedWord(8));
        Assert.Equal(0x77u, completed.Memory.SoundPublishedFlags);
        NativeBattleCurse47IoAdvance waiting = NativeBattleCurse47Io.AdvanceTask(begun.Task, completed.State);
        Assert.Equal((byte)1, waiting.Task.ReadByte(0x29));
        NativeBattleCurse47IoAdvance ready = NativeBattleCurse47Io.AdvanceTask(waiting.Task, waiting.State.Write(NativeBattleCurse47Io.ControlAddress + 0x16A, [1]));
        Assert.Equal((byte)0, ready.Task.ReadByte(0x29));
        Assert.Equal((byte)0, ready.State.Read(NativeBattleCurse47Io.ControlAddress + 0x16A, 1)[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleCurse47Io.Complete(begun.State, NativeBattleEffectMemory.FromColdProcess(), 0x82AFC0));
    }

    [Fact]
    public void Globals_exclude_every_hierarchy_bank_and_descriptor_and_unknown_bytes_remain_unknown()
    {
        NativeBattleCurse47State state = Initialized();
        foreach (NativeBattleCureFamilyPool pool in NativeBattleCurse47State.Pools)
        {
            Assert.Throws<InvalidDataException>(() => state.Read(pool.Bank, 1));
            Assert.Throws<InvalidDataException>(() => state.Write(pool.Bank, [1]));
            Assert.Throws<InvalidDataException>(() => state.Read(pool.Descriptor, 1));
        }
        NativeBattleCurse47State unknown = NativeBattleCurse47State.FromUnknown();
        unknown.Validate();
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(NativeBattleCurse47State.SharedAddress));
        Assert.Throws<NotSupportedException>(() => NativeBattleCurse47Io.Initialize(unknown));
        Assert.Throws<InvalidDataException>(() => NativeBattleCurse47State.FromColdProcess([]));
        Assert.Throws<InvalidDataException>(() => NativeBattleCurse47Io.Initialize(state.WriteDword(NativeBattleCurse47Io.NextAddressGlobal, 0)));
        Assert.Throws<InvalidDataException>(() => state.Write(NativeBattleCurse47Io.ControlAddress + 364, [1]));
    }

    [Fact]
    public void Wait_and_terminator_preserve_task_age_wrap_children_and_current_script()
    {
        NativeBattleCurse47IoAdvance waiting = NativeBattleCurse47Io.AdvanceTask(Task().WriteWord(0x24, 0xFFFF), Initialized());
        Assert.Null(waiting.Request);
        Assert.Equal((ushort)0, waiting.Task.ReadWord(0x24));
        var released = NativeBattleCurse47Io.ReleaseWait(waiting.State);
        Assert.True(released.Released);
        Assert.False(NativeBattleCurse47Io.ReleaseWait(released.State).Released);
        NativeBattleCurse47State end = released.State.WriteDword(NativeBattleCurse47Io.CursorGlobal, NativeBattleCurse47Io.ScriptAddress + 64);
        NativeBattleCurse47IoAdvance terminal = NativeBattleCurse47Io.AdvanceTask(Task().WriteByte(0x28, 1), end);
        Assert.False(terminal.ReadyToComplete);
        NativeBattleCurse47IoAdvance complete = NativeBattleCurse47Io.AdvanceTask(terminal.Task.WriteByte(0x28, 0), terminal.State);
        Assert.True(complete.ReadyToComplete);
        Assert.Equal((byte)2, complete.Task.ReadByte(0x29));
    }

    internal static NativeBattleCurse47State Initialized() => NativeBattleCurse47Io.Initialize(NativeBattleCurse47State.FromColdProcess(Script()).WriteDword(NativeBattleCurse47Io.NextAddressGlobal, Buffer));
    private static NativeBattleModulePoolTask Task()
    {
        NativeBattleCureFamilyPool shape = NativeBattleCurse47State.Pools[7];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
        (pool, int slot) = pool.Append(NativeBattleCurse47Io.Callback);
        return pool.Slots[slot];
    }

    private static ImmutableArray<byte> Script()
    {
        byte[] bytes = new byte[80];
        for (int index = 0; index < 3; index++)
        {
            int row = (index + 1) * 16;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(row), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(row + 2), (ushort)(388 + index));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(row + 8), index == 2 ? 32832 : 33312);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(64), 0xFF);
        return bytes.ToImmutableArray();
    }

    private static byte[] Tim(int length, byte fill)
    {
        byte[] bytes = Enumerable.Repeat(fill, length).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(64), 0x3210);
        return bytes;
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
