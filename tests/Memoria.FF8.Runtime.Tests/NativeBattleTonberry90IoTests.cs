using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90IoTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Control = 0x25A4C28;

    [Fact]
    public void File_copy_uses_actual_payload_length_and_preserves_the_reserved_buffer_tail()
    {
        NativeBattleTonberry90IoAdvance begun = NativeBattleTonberry90Io.Advance762850(Initialize(streamFirst: true), Task());
        NativeBattleTonberry90Frame frame = begun.Frame with { Memory = begun.Frame.Memory with { Arena = begun.Frame.Memory.Arena!.Write(0x20000, Enumerable.Repeat((byte)0xA5, 128).ToArray()) } };
        NativeBattleTonberry90IoQueued queued = NativeBattleTonberry90Io.CopyAndQueueFile(frame, NativeBattleFileIoState.FromColdProcess(), new(417, "stream", BitConverter.GetBytes(7).ToImmutableArray()));
        Assert.Equal(7, BinaryPrimitives.ReadInt32LittleEndian(queued.Frame.Memory.Arena!.Read(0x20000, 4)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 124), queued.Frame.Memory.Arena.Read(0x20004, 124));
        Assert.Equal(4, queued.FileIo.Rows[0].FileResult);
        Assert.Equal(0x1C30, begun.Request!.DeclaredLength);
    }

    [Fact]
    public void Initialization_preserves_current_script_and_uses_plain_external_directory_without_Choco_presets()
    {
        NativeBattleTonberry90Frame frame = Initialize();
        NativeBattleTonberry90State shared = frame.Memory.Tonberry90!;
        Assert.Equal(0x15474D0u, shared.ReadDword(0x25A2CF8));
        Assert.Equal(Control, shared.ReadDword(0x25A3DEC));
        Assert.Equal(Base + 0x2A000, shared.ReadDword(0x25A4C1C));
        foreach (int offset in new[] { 0x108, 0x12C, 0x130 })
        {
            Assert.Equal(0x19DB018u, shared.ReadDword(Control + (uint)offset));
        }
        byte[] script = shared.Io!.Script.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(12), 0x12345678);
        frame = frame with { Memory = frame.Memory with { Tonberry90 = shared with { Io = shared.Io with { Script = script.ToImmutableArray() } } } };
        NativeBattleTonberry90Frame repeated = NativeBattleTonberry90Io.Initialize762760(frame, Script());
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(repeated.Memory.Tonberry90!.Io!.Script.AsSpan().Slice(12)));
        Assert.Equal(Base + 0x2A000, repeated.Memory.Tonberry90.ReadDword(Control + 0x100));
    }

    [Fact]
    public void Async_file_completion_reads_current_stream_destination_then_publishes_ready_synchronously()
    {
        NativeBattleTonberry90Frame frame = Initialize(streamFirst: true);
        NativeBattleTonberry90IoAdvance begun = NativeBattleTonberry90Io.Advance762850(frame, Task());
        frame = begun.Frame;
        Assert.Equal((ushort)417, begun.Request!.FileId);
        Assert.Equal(0x762A80u, begun.Request.CompletionCallback);
        Assert.Equal((ushort)1, frame.Memory.Tonberry90!.ReadWord(Control + 0x164));
        Assert.Equal((ushort)0, frame.Memory.Tonberry90.ReadWord(Control + 0x16A));
        byte[] file = new byte[0x1C30];
        BinaryPrimitives.WriteInt32LittleEndian(file, 5);
        NativeBattleTonberry90IoQueued queued = NativeBattleTonberry90Io.CopyAndQueueFile(frame, NativeBattleFileIoState.FromColdProcess(), new(417, "stream", file.ToImmutableArray()));
        frame = queued.Frame;
        Assert.Equal((short?)1, queued.FileIo.ActiveCount);
        Assert.Equal(0, frame.Memory.SummonStream!.Number);
        NativeBattleTonberry90IoAdvance busy = NativeBattleTonberry90Io.Advance762850(frame, begun.Task);
        Assert.Equal((byte)1, busy.Task.ReadByte(0x29));
        Assert.Equal(0x15474D0u, busy.Frame.Memory.Tonberry90!.ReadDword(0x25A2CF8));
        NativeBattleTonberry90State shared = frame.Memory.Tonberry90!;
        byte[] script = shared.Io!.Script.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(12), Base + 0x20100);
        shared = shared with { Io = shared.Io with { Script = script.ToImmutableArray() } };
        frame = frame with { Memory = frame.Memory with { Tonberry90 = shared, Arena = frame.Memory.Arena!.Write(0x20100, BitConverter.GetBytes(9)) } };
        NativeBattleTonberry90Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleTonberry90Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = NativeBattleTonberry90Io.Complete(frame, 0x762A80);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(NativeBattleTonberry90Io.Complete(restored, 0x762A80)));
        Assert.Equal(690009, frame.Memory.SummonStream!.Number);
        Assert.Equal(Base + 0x20100, frame.Memory.SummonStream.SourceAddress);
        Assert.Equal((ushort)0, frame.Memory.Tonberry90!.ReadWord(Control + 0x164));
        Assert.Equal((ushort)1, frame.Memory.Tonberry90.ReadWord(Control + 0x16A));
        NativeBattleTonberry90IoAdvance polled = NativeBattleTonberry90Io.Advance762850(frame, busy.Task);
        Assert.Equal((byte)0, polled.Task.ReadByte(0x29));
        Assert.Equal(0x15474E0u, polled.Frame.Memory.Tonberry90!.ReadDword(0x25A2CF8));
        Assert.Equal((ushort)0, polled.Frame.Memory.Tonberry90.ReadWord(Control + 0x16A));
    }

    [Fact]
    public void Stream_poll_requires_both_busy_clear_and_ready_and_does_not_manufacture_completion()
    {
        NativeBattleTonberry90IoAdvance begun = NativeBattleTonberry90Io.Advance762850(Initialize(streamFirst: true), Task());
        NativeBattleTonberry90State shared = begun.Frame.Memory.Tonberry90!.Write(Control + 0x164, new byte[] { 0, 0 });
        NativeBattleTonberry90Frame frame = begun.Frame with { Memory = begun.Frame.Memory with { Tonberry90 = shared } };
        NativeBattleTonberry90IoAdvance pending = NativeBattleTonberry90Io.Advance762850(frame, begun.Task);
        Assert.Equal((byte)1, pending.Task.ReadByte(0x29));
        Assert.Equal(0x15474D0u, pending.Frame.Memory.Tonberry90!.ReadDword(0x25A2CF8));
        shared = shared.Write(Control + 0x16A, new byte[] { 1 });
        frame = frame with { Memory = frame.Memory with { Tonberry90 = shared } };
        Assert.Equal((byte)0, NativeBattleTonberry90Io.Advance762850(frame, begun.Task).Task.ReadByte(0x29));
    }

    [Fact]
    public void Wait_release_and_cold_factory_preserve_real_process_owners()
    {
        NativeBattleTonberry90Frame frame = Initialize();
        Assert.Equal((uint?)0x25A3DF0, frame.Memory.Tonberry90!.DirectorAddress1547168);
        Assert.Equal((uint?)0x25A3D00, frame.Memory.Tonberry90.CameraAddress1547510);
        var released = NativeBattleTonberry90Io.Release763C10(frame);
        Assert.True(released.Released);
        Assert.Equal(0x15474E0u, released.Frame.Memory.Tonberry90!.ReadDword(0x25A2CF8));
        Assert.False(NativeBattleTonberry90Io.Release763C10(released.Frame).Released);
        Assert.Null(new NativeBattleEffectMemory(null, 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0).Tonberry90);
    }

    private static NativeBattleTonberry90Frame Initialize(bool streamFirst = false)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        memory = memory with { Tonberry90 = memory.Tonberry90!.WriteDword(0x25A4C1C, Base + 0x20000) };
        return NativeBattleTonberry90Io.Initialize762760(new(memory, new(90, 89, new(90, 0x762360, 0x762320), null, null), new(1)), Script(streamFirst));
    }

    private static ImmutableArray<byte> Script(bool streamFirst = false)
    {
        byte[] script = new byte[64];
        if (streamFirst)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(script, 6);
            BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(2), 417);
            BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(8), 0x1C30);
            BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(16), 0xFF);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(16), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(18), 416);
            BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(24), 0x3500);
            BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(32), 6);
            BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(34), 417);
            BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(40), 0x1C30);
            BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(48), 0xFF);
        }
        return script.ToImmutableArray();
    }

    private static NativeBattleModulePoolTask Task()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25A4BF0, 0x25A3FA8, 0x30, 2);
        (pool, int slot) = pool.Append(0x762850);
        return pool.Slots[slot];
    }
}
