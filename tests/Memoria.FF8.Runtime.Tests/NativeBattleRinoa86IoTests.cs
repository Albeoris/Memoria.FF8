using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa86IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;

    [Fact]
    public void Original_control_presets_wait_release_and_TIM_destination_write_preserve_the_retained_script_tail()
    {
        ImmutableArray<byte> script = Script();
        NativeBattleRinoa86IoState state = NativeBattleRinoa86Io.Initialize77F680(script, Buffer);
        byte[] expected = new byte[364];
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(256), Buffer);
        for (int offset = 260; offset <= 352; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(offset), 0x19CF018);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(276), 0x19EB018);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(300), 0x19D2560);
        Assert.Equal(expected, state.Control.ToArray());
        Assert.Equal(script.ToArray(), state.Script.ToArray());
        NativeBattleRinoa86IoAdvance waiting = NativeBattleRinoa86Io.Advance77F780(Task().WriteWord(36, ushort.MaxValue), state);
        Assert.Null(waiting.Request);
        Assert.Equal((ushort)0, waiting.Task.ReadWord(36));
        NativeBattleRinoa86IoRelease release = NativeBattleRinoa86Io.ReleaseWait77FDB0(waiting.State);
        Assert.True(release.Released);
        Assert.Equal((ushort)1, release.State.WaitReleased);
        Assert.Equal(0x1565EC0u, release.State.CursorAddress);
        Assert.False(NativeBattleRinoa86Io.ReleaseWait77FDB0(release.State).Released);
        NativeBattleRinoa86IoAdvance begun = NativeBattleRinoa86Io.Advance77F780(waiting.Task, release.State);
        Assert.Equal(new NativeBattleRinoa86IoRequest(405, Buffer, 33312, 0x77F980), begun.Request);
        Assert.Equal((ushort)1, begun.State.Busy);
        Assert.Equal((byte)1, begun.Task.ReadByte(41));
        Assert.Equal(Buffer, BinaryPrimitives.ReadUInt32LittleEndian(begun.State.Script.AsSpan().Slice(28)));
        Assert.Equal(0xAABBCCDDu, BinaryPrimitives.ReadUInt32LittleEndian(script.AsSpan().Slice(28)));
        Assert.Equal(0xAABBCCDDu, BinaryPrimitives.ReadUInt32LittleEndian(begun.State.Script.AsSpan().Slice(44)));
    }

    [Fact]
    public void Asynchronous_copy_and_restore_keep_the_queue_consumer_busy_until_the_real_TIM_completion()
    {
        NativeBattleRinoa86IoState state = NativeBattleRinoa86Io.ReleaseWait77FDB0(NativeBattleRinoa86Io.Initialize77F680(Script(), Buffer)).State;
        NativeBattleRinoa86IoAdvance begun = NativeBattleRinoa86Io.Advance77F780(Task(), state);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(86);
        NativeBattleEffectFileAsset file = new(405, "fixture", Enumerable.Repeat((byte)0xA5, 33312).ToImmutableArray());
        NativeBattleFileIoState queue = NativeBattleFileIoState.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => NativeBattleRinoa86Io.CopyAndQueueFile(begun.State, memory, queue, file with { FileId = 406 }));
        NativeBattleRinoa86IoQueued queued = NativeBattleRinoa86Io.CopyAndQueueFile(begun.State, memory, queue, file);
        Assert.Equal(file.Data.ToArray(), queued.Memory.Arena!.Read(0x20000, file.Data.Length));
        Assert.Equal((short)1, queued.FileIo.ActiveCount);
        Assert.Equal(0x77F980u, queued.FileIo.Rows[0].CompletionCallback);
        Assert.Equal(0, queued.Memory.Textures!.Count);
        NativeBattleRinoa86IoAdvance poll = NativeBattleRinoa86Io.Advance77F780(begun.Task, begun.State);
        Assert.Equal((ushort)1, poll.State.Busy);
        Assert.Equal(begun.State.CursorAddress, poll.State.CursorAddress);
        NativeBattleRinoa86IoState restored = JsonSerializer.Deserialize<NativeBattleRinoa86IoState>(JsonSerializer.Serialize(begun.State))!;
        Assert.Equal(JsonSerializer.Serialize(poll), JsonSerializer.Serialize(NativeBattleRinoa86Io.Advance77F780(begun.Task, restored)));
    }

    [Fact]
    public void FF_completes_the_original_task_only_when_its_own_children_have_released()
    {
        NativeBattleRinoa86IoState state = NativeBattleRinoa86Io.Initialize77F680(Script(), Buffer) with { CursorAddress = 0x1565EE0 };
        NativeBattleRinoa86IoAdvance complete = NativeBattleRinoa86Io.Advance77F780(Task(), state);
        Assert.True(complete.ReadyToComplete);
        Assert.Equal((byte)1, complete.Task.ReadByte(41));
        Assert.Equal(1, complete.Task.ReadByte(38) & 1);
        Assert.False(NativeBattleRinoa86Io.Advance77F780(Task().WriteByte(40, 1), state).ReadyToComplete);
        Assert.Throws<InvalidDataException>(() => NativeBattleRinoa86Io.Initialize77F680(default, Buffer));
        Assert.Throws<InvalidDataException>(() => NativeBattleRinoa86Io.Initialize77F680(Script(), 0));
    }

    internal static ImmutableArray<byte> Script()
    {
        byte[] bytes = new byte[64];
        for (int index = 0; index < 2; index++)
        {
            int row = (index + 1) * 16;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(row), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(row + 2), (ushort)(405 + index));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(row + 8), 33312);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(row + 12), 0xAABBCCDD);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(48), 255);
        return bytes.ToImmutableArray();
    }
    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x77F780, new byte[36].ToImmutableArray());
}
