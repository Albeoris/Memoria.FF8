using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco97IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;

    [Fact]
    public void Original_IO_profile_retains_complete_mutable_script_and_twelve_exact_BATTLE_files()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleChoco97IoResources resources = game.ReadNativeChoco97IoResources();
        Assert.Same(resources, game.ReadNativeChoco97IoResources());
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO97_IO_EXPORT") is { } export)
        {
            Directory.CreateDirectory(export);
            var files = resources.Files.Where(file => file.FileId is 445 or 448 or 449 or 450).ToArray();
            foreach (NativeBattleEffectFileAsset file in files)
            {
                using FileStream stream = new(Path.Combine(export, $"data-{file.FileId}.bin"), FileMode.CreateNew);
                stream.Write(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan());
            }
            File.WriteAllText(Path.Combine(export, "manifest.json"), JsonSerializer.Serialize(files.Select(file => new { file.FileId, file.Name, Bytes = NativeBattleStorageSnapshots.Capture(file).Data.Length, Sha256 = Convert.ToHexString(SHA256.HashData(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan())) }), new JsonSerializerOptions { WriteIndented = true }));
        }

        Assert.Equal("FF8_EN:152B930", resources.Script.Identity);
        Assert.Equal(256, NativeBattleStorageSnapshots.Capture(resources.Script).Data.Length);
        Assert.Equal(Enumerable.Range(442, 12).Select(value => (short)value).ToArray(), resources.Files.Select(file => file.FileId).ToArray());
        Assert.All(resources.Files, file => Assert.StartsWith("MAG096_B.", file.Name));
        ushort[] opcodes = Enumerable.Range(0, 16).Select(row => U16(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16)).ToArray();
        Assert.Equal(3, opcodes.Count(opcode => opcode == 0));
        Assert.Equal(8, opcodes.Count(opcode => opcode == 1));
        Assert.Equal(1, opcodes.Count(opcode => opcode == 2));
        Assert.Equal(3, opcodes.Count(opcode => opcode == 3));
        Assert.Equal((ushort)0xFF, opcodes[^1]);
        int index = 0;
        for (int row = 0; row < 16; row++)
        {
            Assert.Equal(0u, U32(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16 + 12));
            if (opcodes[row] is 1 or 2 or 3)
            {
                Assert.Equal(checked((ushort)resources.Files[index].FileId), U16(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16 + 2));
                Assert.Equal(NativeBattleStorageSnapshots.Capture(resources.Files[index]).Data.Length, checked((int)U32(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16 + 8)));
                index++;
            }
        }
        Assert.Equal(12, index);
        string json = NativeBattleSnapshotJson.Serialize(resources);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleChoco97IoResources>(json)));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97IoResourcesReader.Read(new byte[128], []));
    }

    [Fact]
    public void Original_script_completes_twelve_async_files_with_actual_TIMs_adjacent_physical_banks_and_pending_JSON_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleChoco97IoResources resources = game.ReadNativeChoco97IoResources();
        // Explicit diagnostic allocation only; no native bank capacity is inferred from file extents.
        NativeBattleChoco97IoBanks banks = new(0x19DB018, 0x20000, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x20000));
        NativeBattleChoco97IoInitialization initialized = NativeBattleChoco97Io.Initialize729EF0(NativeBattleStorageSnapshots.Capture(resources.Script).Data, Buffer, banks);
        Pending current = new(initialized.State, new(1, 0, null, 0x729FF0, new byte[0x30 - 12].ToImmutableArray()), NativeBattleEffectMemory.FromColdProcess().BeginLoad(97), NativeBattleFileIoState.FromColdProcess());
        int barriers = 0;
        int timCompletions = 0;
        int dataCompletions = 0;
        int files = 0;
        List<uint> ranges = [];
        while (U16(current.State.Script, checked((int)(current.State.CursorAddress - NativeBattleChoco97IoState.ScriptAddress))) != 0xFF)
        {
            int cursor = checked((int)(current.State.CursorAddress - NativeBattleChoco97IoState.ScriptAddress));
            ushort opcode = U16(current.State.Script, cursor);
            if (opcode == 0)
            {
                NativeBattleChoco97IoAdvance waiting = NativeBattleChoco97Io.Advance729FF0(current.Task, current.State);
                Assert.Null(waiting.Request);
                Assert.Equal(current.State.CursorAddress, waiting.State.CursorAddress);
                Assert.Equal((ushort)0, waiting.State.WaitReleased);
                NativeBattleChoco97IoRelease released = NativeBattleChoco97Io.ReleaseWait730590(waiting.State);
                Assert.True(released.Released);
                current = current with { State = released.State, Task = waiting.Task };
                barriers++;
                continue;
            }
            NativeBattleChoco97IoAdvance begun = NativeBattleChoco97Io.Advance729FF0(current.Task, current.State);
            NativeBattleChoco97IoRequest request = Assert.IsType<NativeBattleChoco97IoRequest>(begun.Request);
            NativeBattleEffectFileAsset file = Assert.Single(resources.Files, file => file.FileId == request.FileId);
            Assert.Equal(opcode, request.Opcode);
            Assert.Equal(NativeBattleStorageSnapshots.Capture(file).Data.Length, request.DeclaredLength);
            Assert.Equal(current.State.CursorAddress, begun.State.CursorAddress);
            if (opcode is 2 or 3)
            {
                Assert.Equal(current.State.RangeCount + 1, begun.State.RangeCount);
                int range = begun.State.RangeCount - 1;
                Assert.Equal(request.DestinationAddress, U32(begun.State.Control, range * 4));
                Assert.Equal(unchecked(request.DestinationAddress + (uint)NativeBattleStorageSnapshots.Capture(file).Data.Length), U32(begun.State.Control, 0x80 + range * 4));
                ranges.Add(request.DestinationAddress);
            }
            NativeBattleChoco97IoQueued copied = NativeBattleChoco97Io.CopyAndQueueFile(begun.State, current.Memory, current.FileIo, file);
            Assert.Equal((ushort)1, copied.State.Busy);
            Assert.Equal((short)1, copied.FileIo.ActiveCount);
            Assert.Equal(0, copied.Memory.Textures!.Count);
            if (opcode is 2 or 3)
            {
                Assert.Equal(NativeBattleStorageSnapshots.Capture(file).Data.ToArray(), copied.State.Banks.Read(request.DestinationAddress, NativeBattleStorageSnapshots.Capture(file).Data.Length));
            }
            if (file.FileId == 449)
            {
                ImmutableArray<byte> old = NativeBattleStorageSnapshots.Capture(Assert.Single(resources.Files, file => file.FileId == 445)).Data;
                Assert.Equal(old.ToArray(), copied.State.Banks.Read(0x19DB018, old.Length));
                Assert.Throws<NotSupportedException>(() => copied.State.Banks.Read(0x19E8660, 1));
            }
            Pending pending = new(copied.State, begun.Task, copied.Memory, copied.FileIo);
            Pending cold = NativeBattleSnapshotJson.Deserialize<Pending>(NativeBattleSnapshotJson.Serialize(pending))!;
            int callbacks = pending.FileIo.Rows.Single(row => row.SchedulerCallback != 0).Countdown!.Value + 1;
            for (int callback = 0; callback < callbacks; callback++)
            {
                pending = Pump(pending);
                cold = Pump(cold);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(pending), NativeBattleSnapshotJson.Serialize(cold));
                if (callback + 1 < callbacks)
                {
                    Assert.Equal((ushort)1, pending.State.Busy);
                    Assert.Equal(0, pending.Memory.Textures!.Count);
                }
            }
            Assert.Equal((ushort)0, pending.State.Busy);
            Assert.Equal((short)0, pending.FileIo.ActiveCount);
            Assert.Equal(begun.State.CursorAddress, pending.State.CursorAddress);
            if (opcode == 1)
            {
                Assert.Equal(1, pending.Memory.Textures!.Count);
                NativeBattleEffectTextureDrain drained = pending.Memory.Textures.Drain505D20(pending.Memory.Arena);
                Assert.NotEmpty(drained.Uploads);
                pending = pending with { Memory = pending.Memory with { Textures = drained.State } };
                timCompletions++;
            }
            else
            {
                Assert.Equal(0, pending.Memory.Textures!.Count);
                dataCompletions++;
            }
            NativeBattleChoco97IoAdvance polled = NativeBattleChoco97Io.Advance729FF0(pending.Task, pending.State);
            Assert.Equal(pending.State.CursorAddress + 16, polled.State.CursorAddress);
            Assert.Equal((byte)0, polled.Task.ReadByte(0x29));
            current = pending with { State = polled.State, Task = polled.Task };
            files++;
        }
        Assert.Equal(3, barriers);
        Assert.Equal(12, files);
        Assert.Equal(8, timCompletions);
        Assert.Equal(4, dataCompletions);
        Assert.Equal(new uint[] { 0x19DB018, 0x19DFEF8, 0x19E4958, 0x19E8660 }, ranges);
        short[] dataFileIds = [445, 448, 449, 450];
        for (int index = 0; index < dataFileIds.Length; index++)
        {
            int length = NativeBattleStorageSnapshots.Capture(Assert.Single(resources.Files, file => file.FileId == dataFileIds[index])).Data.Length;
            Assert.Equal(index + 1 < ranges.Count ? ranges[index + 1] : 0x19EEA80u, ranges[index] + (uint)length);
        }
        Assert.Equal((short)4, current.State.RangeCount);
        Assert.True(NativeBattleChoco97Io.Advance729FF0(current.Task, current.State).ReadyToComplete);
        NativeBattleChoco97IoInitialization warm = NativeBattleChoco97Io.Initialize729EF0(current.State.Script, initialized.NextAddress, current.State.Banks);
        Assert.Equal(current.State.Script.ToArray(), warm.State.Script.ToArray());
        Assert.Equal(initialized.NextAddress, warm.State.BufferAddress);
        Assert.Equal(0u, U32(NativeBattleStorageSnapshots.Capture(resources.Script).Data, 28));
        Assert.Equal(Buffer, U32(current.State.Script, 28));
    }

    private static Pending Pump(Pending pending)
    {
        NativeBattleChoco97IoState state = pending.State;
        NativeBattleEffectMemory memory = pending.Memory;
        NativeBattleFileIoState fileIo = pending.FileIo.Advance482590((current, completion) =>
        {
            NativeBattleChoco97IoCompletion completed = NativeBattleChoco97Io.Complete(state, memory, completion.Callback);
            state = completed.State;
            memory = completed.Memory;
            return current;
        });
        return pending with { State = state, Memory = memory, FileIo = fileIo };
    }
    private static ushort U16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private sealed record Pending(NativeBattleChoco97IoState State, NativeBattleModulePoolTask Task, NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);
}
