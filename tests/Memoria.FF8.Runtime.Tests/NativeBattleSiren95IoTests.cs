using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95IoTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Control = 0x258FB88;

    [Fact]
    public void File_copy_uses_actual_payload_length_and_preserves_the_reserved_buffer_tail()
    {
        NativeBattleSiren95IoAdvance begun = NativeBattleSiren95Io.Advance73A380(Initialize(streamFirst: true), Task());
        NativeBattleSiren95Frame frame = begun.Frame with { Memory = begun.Frame.Memory with { Arena = begun.Frame.Memory.Arena!.Write(0x20000, Enumerable.Repeat((byte)0xA5, 128).ToArray()) } };
        NativeBattleSiren95IoQueued queued = NativeBattleSiren95Io.CopyAndQueueFile(frame, NativeBattleFileIoState.FromColdProcess(), new(417, "stream", BitConverter.GetBytes(7).ToImmutableArray()));
        Assert.Equal(7, BinaryPrimitives.ReadInt32LittleEndian(queued.Frame.Memory.Arena!.Read(0x20000, 4)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 124), queued.Frame.Memory.Arena.Read(0x20004, 124));
        Assert.Equal(4, queued.FileIo.Rows[0].FileResult);
        Assert.Equal(0x1C30, begun.Request!.DeclaredLength);
    }

    [Fact]
    public void Initialization_preserves_current_script_and_original_external_directory_overrides()
    {
        NativeBattleSiren95Frame frame = Initialize();
        NativeBattleSiren95State shared = frame.Memory.Siren95!;
        Assert.Equal(0x1533850u, shared.ReadDword(0x258BFA8));
        Assert.Equal(Control, shared.ReadDword(0x258EAA0));
        Assert.Equal(Base + 0x2A000, shared.ReadDword(0x258FB74));
        Assert.Equal(0x19D1018u, shared.ReadDword(Control + 0x104));
        Assert.Equal(0x19DFCC4u, shared.ReadDword(Control + 0x108));
        Assert.Equal(0x19E10F8u, shared.ReadDword(Control + 0x12C));
        Assert.Equal(0x19EBF30u, shared.ReadDword(Control + 0x130));
        Assert.Equal(0x19F19BCu, shared.ReadDword(Control + 0x13C));
        byte[] script = shared.Io!.Script.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(12), 0x12345678);
        frame = frame with { Memory = frame.Memory with { Siren95 = shared with { Io = shared.Io with { Script = script.ToImmutableArray() } } } };
        NativeBattleSiren95Frame repeated = NativeBattleSiren95Io.Initialize73A270(frame, Script());
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(repeated.Memory.Siren95!.Io!.Script.AsSpan().Slice(12)));
        Assert.Equal(Base + 0x2A000, repeated.Memory.Siren95.ReadDword(Control + 0x100));
    }

    [Fact]
    public void Async_file_completion_reads_current_stream_destination_then_publishes_ready_synchronously()
    {
        NativeBattleSiren95Frame frame = Initialize(streamFirst: true);
        NativeBattleSiren95IoAdvance begun = NativeBattleSiren95Io.Advance73A380(frame, Task());
        frame = begun.Frame;
        Assert.Equal((ushort)417, begun.Request!.FileId);
        Assert.Equal(0x73A5B0u, begun.Request.CompletionCallback);
        Assert.Equal((ushort)1, frame.Memory.Siren95!.ReadWord(Control + 0x164));
        Assert.Equal((ushort)0, frame.Memory.Siren95.ReadWord(Control + 0x16A));
        byte[] file = new byte[0x1C30];
        BinaryPrimitives.WriteInt32LittleEndian(file, 5);
        NativeBattleSiren95IoQueued queued = NativeBattleSiren95Io.CopyAndQueueFile(frame, NativeBattleFileIoState.FromColdProcess(), new(417, "stream", file.ToImmutableArray()));
        frame = queued.Frame;
        Assert.Equal((short?)1, queued.FileIo.ActiveCount);
        Assert.Equal(0, frame.Memory.SummonStream!.Number);
        NativeBattleSiren95IoAdvance busy = NativeBattleSiren95Io.Advance73A380(frame, begun.Task);
        Assert.Equal((byte)1, busy.Task.ReadByte(0x29));
        Assert.Equal(0x1533850u, busy.Frame.Memory.Siren95!.ReadDword(0x258BFA8));
        NativeBattleSiren95State shared = frame.Memory.Siren95!;
        byte[] script = shared.Io!.Script.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(12), Base + 0x20100);
        shared = shared with { Io = shared.Io with { Script = script.ToImmutableArray() } };
        frame = frame with { Memory = frame.Memory with { Siren95 = shared, Arena = frame.Memory.Arena!.Write(0x20100, BitConverter.GetBytes(9)) } };
        NativeBattleSiren95Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSiren95Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = NativeBattleSiren95Io.Complete(frame, 0x73A5B0);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(NativeBattleSiren95Io.Complete(restored, 0x73A5B0)));
        Assert.Equal(690009, frame.Memory.SummonStream!.Number);
        Assert.Equal(Base + 0x20100, frame.Memory.SummonStream.SourceAddress);
        Assert.Equal((ushort)0, frame.Memory.Siren95!.ReadWord(Control + 0x164));
        Assert.Equal((ushort)1, frame.Memory.Siren95.ReadWord(Control + 0x16A));
        NativeBattleSiren95IoAdvance polled = NativeBattleSiren95Io.Advance73A380(frame, busy.Task);
        Assert.Equal((byte)0, polled.Task.ReadByte(0x29));
        Assert.Equal(0x1533860u, polled.Frame.Memory.Siren95!.ReadDword(0x258BFA8));
        Assert.Equal((ushort)0, polled.Frame.Memory.Siren95.ReadWord(Control + 0x16A));
    }

    [Fact]
    public void Stream_poll_requires_both_busy_clear_and_ready_and_does_not_manufacture_completion()
    {
        NativeBattleSiren95IoAdvance begun = NativeBattleSiren95Io.Advance73A380(Initialize(streamFirst: true), Task());
        NativeBattleSiren95State shared = begun.Frame.Memory.Siren95!.Write(Control + 0x164, new byte[] { 0, 0 });
        NativeBattleSiren95Frame frame = begun.Frame with { Memory = begun.Frame.Memory with { Siren95 = shared } };
        NativeBattleSiren95IoAdvance pending = NativeBattleSiren95Io.Advance73A380(frame, begun.Task);
        Assert.Equal((byte)1, pending.Task.ReadByte(0x29));
        Assert.Equal(0x1533850u, pending.Frame.Memory.Siren95!.ReadDword(0x258BFA8));
        shared = shared.Write(Control + 0x16A, new byte[] { 1 });
        frame = frame with { Memory = frame.Memory with { Siren95 = shared } };
        Assert.Equal((byte)0, NativeBattleSiren95Io.Advance73A380(frame, begun.Task).Task.ReadByte(0x29));
    }

    [Fact]
    public void Wait_release_and_cold_factory_preserve_real_process_owners()
    {
        NativeBattleSiren95Frame frame = Initialize();
        Assert.Equal((uint?)0x258EAA8, frame.Memory.Siren95!.DirectorAddress1533010);
        var released = NativeBattleSiren95Io.Release73A950(frame);
        Assert.True(released.Released);
        Assert.Equal(0x1533860u, released.Frame.Memory.Siren95!.ReadDword(0x258BFA8));
        Assert.False(NativeBattleSiren95Io.Release73A950(released.Frame).Released);
        Assert.Null(new NativeBattleEffectMemory(null, 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0).Siren95);
    }

    private static NativeBattleSiren95Frame Initialize(bool streamFirst = false)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        memory = memory with { Siren95 = memory.Siren95!.WriteDword(0x258FB74, Base + 0x20000) };
        return NativeBattleSiren95Io.Initialize73A270(new(memory, new(95, 94, new(95, 0x739DA0, 0x739D80), null, null), new(1)), Script(streamFirst));
    }

    private static ImmutableArray<byte> Script(bool streamFirst = false)
    {
        byte[] script = new byte[0x180];
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
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x258FB48, 0x258EDD8, 0x30, 2);
        (pool, int slot) = pool.Append(0x73A380);
        return pool.Slots[slot];
    }
}
