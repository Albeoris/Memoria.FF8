using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMoogle96IoTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Buffer = Arena + 0x13760;

    [Fact]
    public void Original_four_file_script_preserves_async_busy_cursor_and_TIM_completion_across_pending_JSON_restore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleMoogle96Resources resources = game.ReadNativeMoogle96Resources();
        NativeBattleMoogle96IoInitialization initialized = NativeBattleMoogle96Io.Initialize7322A0(resources.IoScript.Data, Buffer);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x732390, new byte[0x30 - 12].ToImmutableArray());
        NativeBattleMoogle96IoAdvance waiting = NativeBattleMoogle96Io.Advance732390(task, initialized.State);
        Assert.Null(waiting.Request);
        Assert.Equal(NativeBattleMoogle96IoState.ScriptAddress, waiting.State.CursorAddress);
        NativeBattleMoogle96IoRelease released = NativeBattleMoogle96Io.ReleaseWait733A80(waiting.State);
        Assert.True(released.Released);
        Pending current = new(released.State, waiting.Task, NativeBattleEffectMemory.FromColdProcess().BeginLoad(96), NativeBattleFileIoState.FromColdProcess());
        int completions = 0;
        foreach (NativeBattleEffectFileAsset file in resources.Files)
        {
            uint cursor = current.State.CursorAddress;
            NativeBattleMoogle96IoAdvance begun = NativeBattleMoogle96Io.Advance732390(current.Task, current.State);
            NativeBattleMoogle96IoRequest request = Assert.IsType<NativeBattleMoogle96IoRequest>(begun.Request);
            Assert.Equal(file.FileId, request.FileId);
            Assert.Equal(file.Data.Length, request.DeclaredLength);
            Assert.Equal(Buffer, request.DestinationAddress);
            Assert.Equal(0x732590u, request.CompletionCallback);
            NativeBattleMoogle96IoQueued copied = NativeBattleMoogle96Io.CopyAndQueueFile(begun.State, current.Memory, current.FileIo, file);
            Assert.Equal(file.Data.ToArray(), copied.Memory.Arena!.Read(checked((int)(Buffer - Arena)), file.Data.Length));
            Assert.Equal((ushort)1, begun.State.Busy);
            Assert.Equal(0, copied.Memory.Textures!.Count);
            Assert.Equal((short)1, copied.FileIo.ActiveCount);
            Assert.Equal(0x732590u, copied.FileIo.Rows[0].CompletionCallback);
            Assert.Equal(cursor, begun.State.CursorAddress);
            Assert.Equal(Buffer, BinaryPrimitives.ReadUInt32LittleEndian(begun.State.Script.AsSpan().Slice(checked((int)(cursor - NativeBattleMoogle96IoState.ScriptAddress)) + 12)));
            NativeBattleMoogle96IoAdvance polled = NativeBattleMoogle96Io.Advance732390(begun.Task, begun.State);
            Assert.Equal((byte)1, polled.Task.ReadByte(0x29));
            Assert.Null(polled.Request);
            Assert.Equal(cursor, polled.State.CursorAddress);
            Assert.Equal((ushort)1, polled.State.Busy);
            Pending pending = new(polled.State, polled.Task, copied.Memory, copied.FileIo);
            Pending restored = JsonSerializer.Deserialize<Pending>(JsonSerializer.Serialize(pending))!;
            Assert.Equal(JsonSerializer.Serialize(pending), JsonSerializer.Serialize(restored));
            Pending completed = pending;
            int schedulerCallbacks = copied.FileIo.Rows[0].Countdown!.Value + 1;
            for (int callback = 0; callback < schedulerCallbacks; callback++)
            {
                completed = Pump(completed);
                restored = Pump(restored);
                Assert.Equal(JsonSerializer.Serialize(completed), JsonSerializer.Serialize(restored));
                if (callback + 1 < schedulerCallbacks)
                {
                    Assert.Equal((ushort)1, completed.State.Busy);
                    Assert.Equal((short)1, completed.FileIo.ActiveCount);
                    Assert.Equal(0, completed.Memory.Textures!.Count);
                }
            }
            Assert.Equal((ushort)0, completed.State.Busy);
            Assert.Equal(cursor, completed.State.CursorAddress);
            Assert.Equal((byte)1, completed.Task.ReadByte(0x29));
            Assert.Equal((short)0, completed.FileIo.ActiveCount);
            Assert.Equal(1, completed.Memory.Textures!.Count);
            completions++;
            NativeBattleEffectTextureDrain drained = completed.Memory.Textures.Drain505D20(completed.Memory.Arena);
            Assert.NotEmpty(drained.Uploads);
            Assert.Equal(0, drained.State.Count);
            completed = completed with { Memory = completed.Memory with { Textures = drained.State } };
            NativeBattleMoogle96IoAdvance advanced = NativeBattleMoogle96Io.Advance732390(completed.Task, completed.State);
            Assert.Null(advanced.Request);
            Assert.Equal(cursor + 16, advanced.State.CursorAddress);
            Assert.Equal((byte)0, advanced.Task.ReadByte(0x29));
            Assert.False(advanced.ReadyToComplete);
            current = completed with { State = advanced.State, Task = advanced.Task };
        }
        Assert.Equal(4, completions);
        NativeBattleMoogle96IoAdvance terminal = NativeBattleMoogle96Io.Advance732390(current.Task, current.State);
        Assert.True(terminal.ReadyToComplete);
        Assert.Null(terminal.Request);
        Assert.Equal((ushort)0, terminal.State.WaitReleased);
        Assert.Equal(NativeBattleMoogle96IoState.ScriptAddress + 80, terminal.State.CursorAddress);
        NativeBattleMoogle96IoInitialization nextCast = NativeBattleMoogle96Io.Initialize7322A0(terminal.State.Script, initialized.NextAddress);
        Assert.Equal(terminal.State.Script.ToArray(), nextCast.State.Script.ToArray());
        Assert.Equal(initialized.NextAddress, nextCast.State.BufferAddress);
        for (int index = 1; index <= 4; index++)
        {
            Assert.Equal(Buffer, BinaryPrimitives.ReadUInt32LittleEndian(nextCast.State.Script.AsSpan().Slice(index * 16 + 12)));
        }
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.IoScript.Data.AsSpan().Slice(16 + 12)));
    }

    private static Pending Pump(Pending pending)
    {
        NativeBattleMoogle96IoState state = pending.State;
        NativeBattleEffectMemory memory = pending.Memory;
        NativeBattleFileIoState fileIo = pending.FileIo.Advance482590((current, completion) =>
        {
            Assert.Equal(0x732590u, completion.Callback);
            NativeBattleMoogle96IoCompletion completed = NativeBattleMoogle96Io.Complete732590(state, memory);
            state = completed.State;
            memory = completed.Memory;
            return current;
        });
        return pending with { State = state, Memory = memory, FileIo = fileIo };
    }

    private sealed record Pending(NativeBattleMoogle96IoState State, NativeBattleModulePoolTask Task, NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);
}
