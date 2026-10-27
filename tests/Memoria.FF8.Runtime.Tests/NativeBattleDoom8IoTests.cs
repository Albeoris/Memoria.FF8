using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8IoTests
{
    private const uint Buffer = NativeBattleEffectNodeMemory.ArenaAddress + 0x20000;

    [Fact]
    public void Initializer_uses_exact_directory_presets_and_preserves_retained_script_destinations()
    {
        byte[] script = Script().ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(28), 0x12345678);
        NativeBattleDoom8IoInitialization initialized = NativeBattleDoom8Io.Initialize8C4E60(script.ToImmutableArray(), Buffer);
        Assert.Equal(Buffer + 0xA000, initialized.NextAddress);
        Assert.Equal(NativeBattleDoom8Io.ScriptAddress, initialized.State.CursorAddress);
        Assert.Equal(script, initialized.State.Script.ToArray());
        Assert.Equal(Buffer, Dword(initialized.State.Control, 0x100));
        Assert.All(initialized.State.Control.Take(0x100), value => Assert.Equal((byte)0, value));
        for (int offset = 0x104; offset <= 0x160; offset += 4)
        {
            uint expected = offset switch
            {
                >= 0x114 and <= 0x124 => 0x19EB018,
                0x128 => 0x19ECBB8,
                0x12C => 0x19ED83C,
                _ => 0x19F0324
            };
            Assert.Equal(expected, Dword(initialized.State.Control, offset));
        }
        Assert.All(initialized.State.Control.Skip(0x164), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void Record_zero_wait_releases_once_without_advancing_task_phase_or_fabricating_a_request()
    {
        NativeBattleProfiledIoState state = Initialize();
        NativeBattleModulePoolTask task = Task().WriteWord(0x24, 0xFFFF);
        NativeBattleProfiledIoAdvance waiting = NativeBattleDoom8Io.Advance8C4F60(task, state);
        Assert.Null(waiting.Request);
        Assert.Equal((ushort)0, waiting.Task.ReadWord(0x24));
        Assert.Equal((byte)0, waiting.Task.ReadByte(0x29));
        Assert.Equal(NativeBattleDoom8Io.ScriptAddress, waiting.State.CursorAddress);
        var released = NativeBattleDoom8Io.Release8C5550(waiting.State);
        Assert.True(released.Released);
        Assert.Equal(NativeBattleDoom8Io.ScriptAddress + 16, released.State.CursorAddress);
        Assert.Equal((ushort)1, Word(released.State.Control, 0x166));
        Assert.False(NativeBattleDoom8Io.Release8C5550(released.State).Released);
        Assert.Equal((byte)0, waiting.Task.ReadByte(0x29));
    }

    [Fact]
    public void Actual_five_record_sequence_uses_async_callbacks_external_shared_destinations_and_TIM_completion()
    {
        NativeBattleProfiledIoState state = NativeBattleDoom8Io.Release8C5550(Initialize()).State;
        NativeBattleModulePoolTask task = Task();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x20000, Enumerable.Repeat((byte)0xA5, 0xA000).ToArray()) };
        NativeBattleFileIoState io = NativeBattleFileIoState.FromColdProcess();
        NativeBattleFileStaging external = NativeBattleFileStaging.Unknown();
        ushort[] ids = [383, 384, 385, 386, 387];
        uint[] destinations = [0x19EB018, 0x19ECBB8, 0x19ED83C, Buffer, Buffer];
        int[] lengths = [7072, 3204, 10984, 33312, 14880];
        int initialUploads = memory.Textures!.Count;
        for (int index = 0; index < ids.Length; index++)
        {
            NativeBattleProfiledIoAdvance begun = NativeBattleDoom8Io.Advance8C4F60(task, state);
            state = begun.State;
            task = begun.Task;
            NativeBattleProfiledIoRequest request = Assert.IsType<NativeBattleProfiledIoRequest>(begun.Request);
            Assert.Equal(ids[index], request.FileId);
            Assert.Equal(destinations[index], request.DestinationAddress);
            Assert.Equal(lengths[index], request.DeclaredLength);
            Assert.Equal(index < 3 ? 0x8C51D0u : 0x8C5160u, request.CompletionCallback);
            Assert.Equal((ushort)1, Word(state.Control, 0x164));
            Assert.Equal(destinations[index], Dword(state.Script, (index + 1) * 16 + 12));
            int uploadsBefore = memory.Textures!.Count;
            byte[] payload = index < 3 ? Enumerable.Repeat(unchecked((byte)ids[index]), lengths[index]).ToArray() : Tim(lengths[index], unchecked((byte)ids[index]));
            var queued = NativeBattleDoom8Io.CopyAndQueueFile(state, memory, io, new(checked((short)ids[index]), "data", payload.ToImmutableArray()), (selected, bytes) =>
            {
                external = external.Write(selected.DestinationAddress, bytes.AsSpan());
            });
            memory = queued.Memory;
            io = queued.FileIo;
            Assert.Equal((short?)1, io.ActiveCount);
            Assert.Equal(lengths[index], io.Rows[0].FileResult);
            Assert.Equal(uploadsBefore, memory.Textures!.Count);
            if (index < 3)
            {
                Assert.Equal(payload, external.Read(destinations[index], lengths[index]));
                Assert.Equal(destinations[index], Dword(state.Control, index * 4));
                Assert.Equal(destinations[index] + (uint)lengths[index], Dword(state.Control, 0x80 + index * 4));
            }
            NativeBattleProfiledIoAdvance busy = NativeBattleDoom8Io.Advance8C4F60(task, state);
            Assert.Equal(state.CursorAddress, busy.State.CursorAddress);
            Assert.Equal((byte)1, busy.Task.ReadByte(0x29));
            Assert.Null(busy.Request);
            task = busy.Task;
            int completionCount = 0;
            int callbacks = 0;
            while (io.ActiveCount != 0)
            {
                Assert.True(callbacks++ < 8);
                io = io.Advance482590((current, completed) =>
                {
                    Assert.Equal(request.CompletionCallback, completed.Callback);
                    var completedState = NativeBattleDoom8Io.Complete(state, memory, completed.Callback);
                    state = completedState.State;
                    memory = completedState.Memory;
                    completionCount++;
                    return current;
                });
            }
            Assert.Equal(1, completionCount);
            Assert.Equal((ushort)0, Word(state.Control, 0x164));
            Assert.Equal(uploadsBefore + (index < 3 ? 0 : 1), memory.Textures!.Count);
            NativeBattleProfiledIoAdvance polled = NativeBattleDoom8Io.Advance8C4F60(task, state);
            Assert.Equal((byte)0, polled.Task.ReadByte(0x29));
            Assert.Equal(state.CursorAddress + 16, polled.State.CursorAddress);
            state = polled.State;
            task = polled.Task;
        }
        NativeBattleProfiledIoAdvance final = NativeBattleDoom8Io.Advance8C4F60(task, state);
        Assert.True(final.ReadyToComplete);
        Assert.Null(final.Request);
        Assert.Equal((byte)1, final.Task.ReadByte(0x29));
        Assert.Equal((byte)1, (byte)(final.Task.ReadByte(0x26) & 1));
        Assert.Equal((ushort)0, Word(final.State.Control, 0x166));
        Assert.Equal((ushort)3, Word(final.State.Control, 0x168));
        Assert.Equal((ushort)16, final.Task.ReadWord(0x24));
        Assert.Equal(initialUploads + 2, memory.Textures!.Count);
        Assert.Equal(new byte[] { 0xA5 }, memory.Arena!.Read(0x20000 + 33312, 1));
        Assert.Equal(new byte[] { 0x82 }, memory.Arena!.Read(0x20000 + 20000, 1));
    }

    [Fact]
    public void Restored_pending_state_and_task_have_identical_completion_and_next_poll()
    {
        NativeBattleProfiledIoAdvance begun = NativeBattleDoom8Io.Advance8C4F60(Task(), NativeBattleDoom8Io.Release8C5550(Initialize()).State);
        NativeBattleProfiledIoState restoredState = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleProfiledIoState>(SnapshotJson(begun.State))!);
        NativeBattleModulePoolTask restoredTask = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(SnapshotJson(begun.Task))!);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        var first = NativeBattleDoom8Io.Complete(begun.State, memory, 0x8C51D0);
        var second = NativeBattleDoom8Io.Complete(restoredState, memory, 0x8C51D0);
        Assert.Equal(SnapshotJson(first.State), SnapshotJson(second.State));
        NativeBattleProfiledIoAdvance next = NativeBattleDoom8Io.Advance8C4F60(begun.Task, first.State);
        NativeBattleProfiledIoAdvance restoredNext = NativeBattleDoom8Io.Advance8C4F60(restoredTask, second.State);
        Assert.Equal(SnapshotJson(next), SnapshotJson(restoredNext));
        Assert.Equal(0x19EB018u, Dword(restoredNext.State.Script, 28));
        NativeBattleDoom8IoInitialization repeated = NativeBattleDoom8Io.Initialize8C4E60(restoredNext.State.Script, Buffer + 0xA000);
        Assert.Equal(0x19EB018u, Dword(repeated.State.Script, 28));
        Assert.Equal((ushort)0, Word(repeated.State.Control, 0x168));
    }

    [Fact]
    public void Terminator_waits_for_actual_children_and_preserves_raw_links_and_other_task_bytes()
    {
        NativeBattleProfiledIoState state = Initialize() with { CursorAddress = NativeBattleDoom8Io.ScriptAddress + 0x60 };
        NativeBattleModulePoolTask task = Task(capacity: 8).WriteByte(0x28, 1).WriteWord(0x2C, 0xA5A5).SetNext(7);
        NativeBattleProfiledIoAdvance final = NativeBattleDoom8Io.Advance8C4F60(task, state);
        Assert.False(final.ReadyToComplete);
        Assert.Equal(7, final.Task.Next);
        Assert.Equal((ushort)0xA5A5, final.Task.ReadWord(0x2C));
        NativeBattleProfiledIoAdvance released = NativeBattleDoom8Io.Advance8C4F60(final.Task.WriteByte(0x28, 0), final.State);
        Assert.True(released.ReadyToComplete);
        Assert.Equal((byte)2, released.Task.ReadByte(0x29));
        Assert.Equal((ushort)2, released.Task.ReadWord(0x24));
    }

    [Fact]
    public void Invalid_source_extent_selector_binding_and_missing_external_owner_fail_at_the_required_boundary()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8Io.Initialize8C4E60([], Buffer));
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8Io.Initialize8C4E60(Script(), NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length - 0xA000 + 1));
        byte[] script = Script().ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(20), 0xFFFF);
        NativeBattleProfiledIoState invalid = NativeBattleDoom8Io.Release8C5550(NativeBattleDoom8Io.Initialize8C4E60(script.ToImmutableArray(), Buffer).State).State;
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Io.Advance8C4F60(Task(), invalid));
        NativeBattleProfiledIoAdvance begun = NativeBattleDoom8Io.Advance8C4F60(Task(), NativeBattleDoom8Io.Release8C5550(Initialize()).State);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8Io.Complete(begun.State, memory, 0x8C51C0));
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8Io.CopyAndQueueFile(begun.State, memory, NativeBattleFileIoState.FromColdProcess(), new(384, "wrong", [1])));
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Io.CopyAndQueueFile(begun.State, memory, NativeBattleFileIoState.FromColdProcess(), new(383, "data", [1])));
    }

    private static NativeBattleProfiledIoState Initialize() => NativeBattleDoom8Io.Initialize8C4E60(Script(), Buffer).State;
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));

    private static NativeBattleModulePoolTask Task(ushort capacity = 2)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x275F918, 0x275F568, 48, capacity);
        (pool, int slot) = pool.Append(0x8C4F60);
        return pool.Slots[slot];
    }

    private static ImmutableArray<byte> Script()
    {
        byte[] bytes = new byte[0x70];
        ushort[] ids = [383, 384, 385, 386, 387];
        int[] lengths = [7072, 3204, 10984, 33312, 14880];
        for (int index = 0; index < ids.Length; index++)
        {
            int offset = (index + 1) * 16;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), index < 3 ? (ushort)3 : (ushort)1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2), ids[index]);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4), index < 3 ? (ushort)index : (ushort)0);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 8), lengths[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x60), 0xFF);
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
