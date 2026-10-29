using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco98IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;

    [Fact]
    public void Original_IO_profile_retains_complete_mutable_script_and_twelve_exact_BATTLE_files()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleChoco98IoResources resources = game.ReadNativeChoco98IoResources();
        Assert.Same(resources, game.ReadNativeChoco98IoResources());
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO98_IO_EXPORT") is { } export)
        {
            Directory.CreateDirectory(export);
            var files = resources.Files.Where(file => file.FileId is 457 or 460 or 461 or 462 or 463).ToArray();
            foreach (NativeBattleEffectFileAsset file in files)
            {
                using FileStream stream = new(Path.Combine(export, $"data-{file.FileId}.bin"), FileMode.CreateNew);
                stream.Write(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan());
            }
            using FileStream manifest = new(Path.Combine(export, "manifest.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(manifest, files.Select(file => new { file.FileId, file.Name, Bytes = NativeBattleStorageSnapshots.Capture(file).Data.Length, Sha256 = Convert.ToHexString(SHA256.HashData(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan())) }), new JsonSerializerOptions { WriteIndented = true });
        }

        Assert.Equal("FF8_EN:152B2A8", resources.Script.Identity);
        Assert.Equal(288, NativeBattleStorageSnapshots.Capture(resources.Script).Data.Length);
        Assert.Equal(Enumerable.Range(454, 14).Select(value => (short)value).ToArray(), resources.Files.Select(file => file.FileId).ToArray());
        Assert.All(resources.Files, file => Assert.StartsWith("MAG097_B.", file.Name));
        ushort[] opcodes = Enumerable.Range(0, 18).Select(row => U16(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16)).ToArray();
        Assert.Equal(3, opcodes.Count(opcode => opcode == 0));
        Assert.Equal(9, opcodes.Count(opcode => opcode == 1));
        Assert.Equal(1, opcodes.Count(opcode => opcode == 2));
        Assert.Equal(4, opcodes.Count(opcode => opcode == 3));
        Assert.Equal((ushort)0xFF, opcodes[^1]);
        int index = 0;
        for (int row = 0; row < 18; row++)
        {
            Assert.Equal(0u, U32(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16 + 12));
            if (opcodes[row] is 1 or 2 or 3)
            {
                Assert.Equal(checked((ushort)resources.Files[index].FileId), U16(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16 + 2));
                Assert.Equal(NativeBattleStorageSnapshots.Capture(resources.Files[index]).Data.Length, checked((int)U32(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16 + 8)));
                index++;
            }
        }
        Assert.Equal(14, index);
        string json = NativeBattleSnapshotJson.Serialize(resources);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleChoco98IoResources>(json)));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98IoResourcesReader.Read(new byte[128], []));
    }

    [Fact]
    public void Original_script_completes_fourteen_async_files_with_actual_TIMs_adjacent_physical_banks_and_pending_JSON_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleChoco98IoResources resources = game.ReadNativeChoco98IoResources();
        // Explicit diagnostic allocation only; no native bank capacity is inferred from file extents.
        NativeBattleChoco97IoBanks banks = new(0x19DB018, 0x13184, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x13184));
        NativeBattleChoco98IoInitialization initialized = NativeBattleChoco98Io.Initialize721CF0(NativeBattleStorageSnapshots.Capture(resources.Script).Data, Buffer, banks);
        Pending current = new(initialized.State, new(1, 0, null, 0x721DF0, new byte[0x30 - 12].ToImmutableArray()), NativeBattleEffectMemory.FromColdProcess().BeginLoad(98), NativeBattleFileIoState.FromColdProcess());
        int barriers = 0;
        int timCompletions = 0;
        int dataCompletions = 0;
        int files = 0;
        List<uint> ranges = [];
        while (U16(current.State.Script, checked((int)(current.State.CursorAddress - NativeBattleChoco98IoState.ScriptAddress))) != 0xFF)
        {
            int cursor = checked((int)(current.State.CursorAddress - NativeBattleChoco98IoState.ScriptAddress));
            ushort opcode = U16(current.State.Script, cursor);
            if (opcode == 0)
            {
                NativeBattleChoco98IoAdvance waiting = NativeBattleChoco98Io.Advance721DF0(current.Task, current.State);
                Assert.Null(waiting.Request);
                Assert.Equal(current.State.CursorAddress, waiting.State.CursorAddress);
                Assert.Equal((ushort)0, waiting.State.WaitReleased);
                NativeBattleChoco98IoRelease released = NativeBattleChoco98Io.ReleaseWait(waiting.State);
                Assert.True(released.Released);
                current = current with { State = released.State, Task = waiting.Task };
                barriers++;
                continue;
            }
            NativeBattleChoco98IoAdvance begun = NativeBattleChoco98Io.Advance721DF0(current.Task, current.State);
            NativeBattleChoco98IoRequest request = Assert.IsType<NativeBattleChoco98IoRequest>(begun.Request);
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
            NativeBattleChoco98IoQueued copied = NativeBattleChoco98Io.CopyAndQueueFile(begun.State, current.Memory, current.FileIo, file);
            Assert.Equal((ushort)1, copied.State.Busy);
            Assert.Equal((short)1, copied.FileIo.ActiveCount);
            Assert.Equal(0, copied.Memory.Textures!.Count);
            if (opcode is 2 or 3)
            {
                Assert.Equal(NativeBattleStorageSnapshots.Capture(file).Data.ToArray(), copied.State.Banks.Read(request.DestinationAddress, NativeBattleStorageSnapshots.Capture(file).Data.Length));
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
            NativeBattleChoco98IoAdvance polled = NativeBattleChoco98Io.Advance721DF0(pending.Task, pending.State);
            Assert.Equal(pending.State.CursorAddress + 16, polled.State.CursorAddress);
            Assert.Equal((byte)0, polled.Task.ReadByte(0x29));
            current = pending with { State = polled.State, Task = polled.Task };
            files++;
        }
        Assert.Equal(3, barriers);
        Assert.Equal(14, files);
        Assert.Equal(9, timCompletions);
        Assert.Equal(5, dataCompletions);
        Assert.Equal(new uint[] { 0x19DB018, 0x19DFEF8, 0x19E187C, 0x19E5584, 0x19E8E70 }, ranges);
        short[] dataFileIds = [457, 460, 461, 462, 463];
        for (int index = 0; index < dataFileIds.Length; index++)
        {
            int length = NativeBattleStorageSnapshots.Capture(Assert.Single(resources.Files, file => file.FileId == dataFileIds[index])).Data.Length;
            Assert.Equal(index + 1 < ranges.Count ? ranges[index + 1] : 0x19EE19Cu, ranges[index] + (uint)length);
        }
        Assert.Equal((short)5, current.State.RangeCount);
        Assert.True(NativeBattleChoco98Io.Advance721DF0(current.Task, current.State).ReadyToComplete);
        NativeBattleChoco98IoInitialization warm = NativeBattleChoco98Io.Initialize721CF0(current.State.Script, initialized.NextAddress, current.State.Banks);
        Assert.Equal(current.State.Script.ToArray(), warm.State.Script.ToArray());
        Assert.Equal(initialized.NextAddress, warm.State.BufferAddress);
        Assert.Equal(0u, U32(NativeBattleStorageSnapshots.Capture(resources.Script).Data, 28));
        Assert.Equal(Buffer, U32(current.State.Script, 28));
    }

    private static Pending Pump(Pending pending)
    {
        NativeBattleChoco98IoState state = pending.State;
        NativeBattleEffectMemory memory = pending.Memory;
        NativeBattleFileIoState fileIo = pending.FileIo.Advance482590((current, completion) =>
        {
            NativeBattleChoco98IoCompletion completed = NativeBattleChoco98Io.Complete(state, memory, completion.Callback);
            state = completed.State;
            memory = completed.Memory;
            return current;
        });
        return pending with { State = state, Memory = memory, FileIo = fileIo };
    }
    private static ushort U16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private sealed record Pending(NativeBattleChoco98IoState State, NativeBattleModulePoolTask Task, NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);
}
