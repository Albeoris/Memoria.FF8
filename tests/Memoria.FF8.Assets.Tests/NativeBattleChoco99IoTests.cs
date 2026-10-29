using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;

    [Fact]
    public void Original_IO_resources_and_optional_fresh_data_export_keep_exact_file_identities()
    {
        using GameAssets game = Installed();
        NativeBattleChoco99IoResources resources = game.ReadNativeChoco99IoResources();
        Assert.Same(resources, game.ReadNativeChoco99IoResources());
        Assert.Equal(0x1A0, NativeBattleStorageSnapshots.Capture(resources.Script).Data.Length);
        Assert.Equal(Enumerable.Range(468, 21).Select(id => (short)id).ToArray(), resources.Files.Select(file => file.FileId).ToArray());
        ushort[] opcodes = Enumerable.Range(0, 26).Select(row => U16(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16)).ToArray();
        Assert.Equal(4, opcodes.Count(opcode => opcode == 0));
        Assert.Equal(11, opcodes.Count(opcode => opcode == 1));
        Assert.Equal(10, opcodes.Count(opcode => opcode is 2 or 3 or 4));
        Assert.Equal((ushort)0xFF, opcodes[^1]);
        Assert.DoesNotContain(opcodes, opcode => opcode is 5 or 6);
        Assert.All(Enumerable.Range(0, 26), row => Assert.Equal(0u, U32(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16 + 12)));
        string json = NativeBattleSnapshotJson.Serialize(resources);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleChoco99IoResources>(json)));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99IoResourcesReader.Read(new byte[128], []));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO99_IO_EXPORT") is { } export)
        {
            Assert.False(Directory.Exists(export) && Directory.EnumerateFileSystemEntries(export).Any(), "Export requires a fresh empty directory.");
            Directory.CreateDirectory(export);
            short[] ids = Enumerable.Range(0, 26).Where(row => opcodes[row] is 2 or 3 or 4).Select(row => checked((short)U16(NativeBattleStorageSnapshots.Capture(resources.Script).Data, row * 16 + 2))).ToArray();
            NativeBattleEffectFileAsset[] files = ids.Select(id => resources.Files.Single(file => file.FileId == id)).ToArray();
            foreach (NativeBattleEffectFileAsset file in files)
            {
                using FileStream stream = new(Path.Combine(export, $"data-{file.FileId}.bin"), FileMode.CreateNew);
                stream.Write(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan());
            }
            using FileStream manifest = new(Path.Combine(export, "manifest.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(manifest, files.Select(file => new { file.FileId, file.Name, Bytes = NativeBattleStorageSnapshots.Capture(file).Data.Length, Sha256 = Convert.ToHexString(SHA256.HashData(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan())) }), new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [Fact]
    public void Original_script_runs_twenty_one_async_files_four_waits_and_actual_overlapping_data_owners()
    {
        using GameAssets game = Installed();
        NativeBattleChoco99IoResources resources = game.ReadNativeChoco99IoResources();
        NativeBattleChoco97IoBanks banks = new(0x19D3018, 0x1F560, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x1F560));
        NativeBattleChoco99IoInitialization initialized = NativeBattleChoco99Io.Initialize718200(NativeBattleStorageSnapshots.Capture(resources.Script).Data, Buffer, banks);
        Pending current = new(initialized.State, new(1, 0, null, 0x718310, new byte[0x30 - 12].ToImmutableArray()), NativeBattleEffectMemory.FromColdProcess().BeginLoad(99), NativeBattleFileIoState.FromColdProcess());
        Dictionary<short, uint> dataDestinations = new() { [471] = 0x19E3018, [474] = 0x19EB018, [475] = 0x19EED20, [476] = 0x19F0F40, [479] = 0x19D3018, [480] = 0x19DD2D0, [481] = 0x19DE510, [482] = 0x19DF380, [483] = 0x19E2054, [484] = 0x19E4778 };
        int waits = 0;
        int tims = 0;
        int dataFiles = 0;
        List<short> completedIds = [];
        while (U16(current.State.Script, checked((int)(current.State.CursorAddress - NativeBattleChoco99IoState.ScriptAddress))) != 0xFF)
        {
            int cursor = checked((int)(current.State.CursorAddress - NativeBattleChoco99IoState.ScriptAddress));
            ushort opcode = U16(current.State.Script, cursor);
            if (opcode == 0)
            {
                NativeBattleChoco99IoAdvance wait = NativeBattleChoco99Io.Advance718310(current.Task, current.State);
                Assert.Null(wait.Request);
                Assert.Equal(current.State.CursorAddress, wait.State.CursorAddress);
                NativeBattleChoco99IoRelease release = NativeBattleChoco99Io.ReleaseWait(wait.State);
                Assert.True(release.Released);
                current = current with { Task = wait.Task, State = release.State };
                waits++;
                continue;
            }
            NativeBattleChoco99IoAdvance begun = NativeBattleChoco99Io.Advance718310(current.Task, current.State);
            NativeBattleChoco99IoRequest request = Assert.IsType<NativeBattleChoco99IoRequest>(begun.Request);
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == request.FileId);
            Assert.Equal(opcode, request.Opcode);
            Assert.Equal(NativeBattleStorageSnapshots.Capture(file).Data.Length, request.DeclaredLength);
            if (opcode != 1)
            {
                Assert.Equal(dataDestinations[file.FileId], request.DestinationAddress);
                int range = begun.State.RangeCount - 1;
                Assert.Equal(request.DestinationAddress, U32(begun.State.Control, range * 4));
                Assert.Equal(request.DestinationAddress + (uint)NativeBattleStorageSnapshots.Capture(file).Data.Length, U32(begun.State.Control, 0x80 + range * 4));
            }
            NativeBattleChoco99IoQueued copied = NativeBattleChoco99Io.CopyAndQueueFile(begun.State, current.Memory, current.FileIo, file);
            if (opcode != 1)
            {
                Assert.Equal(NativeBattleStorageSnapshots.Capture(file).Data.ToArray(), copied.State.Banks.Read(request.DestinationAddress, NativeBattleStorageSnapshots.Capture(file).Data.Length));
            }
            Pending pending = new(copied.State, begun.Task, copied.Memory, copied.FileIo);
            Pending cold = NativeBattleSnapshotJson.Deserialize<Pending>(NativeBattleSnapshotJson.Serialize(pending))!;
            int ticks = pending.FileIo.Rows.Single(row => row.SchedulerCallback != 0).Countdown!.Value + 1;
            for (int tick = 0; tick < ticks; tick++)
            {
                pending = Pump(pending);
                cold = Pump(cold);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(pending), NativeBattleSnapshotJson.Serialize(cold));
                if (tick + 1 < ticks)
                {
                    Assert.Equal((ushort)1, pending.State.Busy);
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
                tims++;
            }
            else
            {
                Assert.Equal(0, pending.Memory.Textures!.Count);
                dataFiles++;
            }
            NativeBattleChoco99IoAdvance polled = NativeBattleChoco99Io.Advance718310(pending.Task, pending.State);
            Assert.Equal(pending.State.CursorAddress + 16, polled.State.CursorAddress);
            current = pending with { Task = polled.Task, State = polled.State };
            completedIds.Add(file.FileId);
        }
        Assert.Equal(4, waits);
        Assert.Equal(11, tims);
        Assert.Equal(10, dataFiles);
        Assert.Equal(Enumerable.Range(468, 21).Select(id => (short)id).ToArray(), completedIds.ToArray());
        Assert.Equal((short)10, current.State.RangeCount);
        Assert.True(NativeBattleChoco99Io.Advance718310(current.Task, current.State).ReadyToComplete);
        byte[] original471 = NativeBattleStorageSnapshots.Capture(resources.Files.Single(file => file.FileId == 471)).Data.ToArray();
        byte[] expected471 = original471.ToArray();
        foreach (short id in new short[] { 483, 484 })
        {
            NativeBattleEffectFileAsset overwrite = resources.Files.Single(file => file.FileId == id);
            uint begin = Math.Max(dataDestinations[id], dataDestinations[471]);
            uint end = Math.Min(dataDestinations[id] + (uint)NativeBattleStorageSnapshots.Capture(overwrite).Data.Length, dataDestinations[471] + (uint)original471.Length);
            NativeBattleStorageSnapshots.Capture(overwrite).Data.AsSpan(checked((int)(begin - dataDestinations[id])), checked((int)(end - begin))).CopyTo(expected471.AsSpan(checked((int)(begin - dataDestinations[471]))));
        }
        Assert.Equal(expected471, current.State.Banks.Read(dataDestinations[471], expected471.Length));
        Assert.NotEqual(original471, expected471);
        Assert.Throws<NotSupportedException>(() => current.State.Banks.Read(0x19E7EF8, 1));
        NativeBattleChoco99IoInitialization warm = NativeBattleChoco99Io.Initialize718200(current.State.Script, initialized.NextAddress, current.State.Banks);
        Assert.Equal(current.State.Script.ToArray(), warm.State.Script.ToArray());
        Assert.Equal(NativeBattleSnapshotJson.Serialize(current.State.Banks), NativeBattleSnapshotJson.Serialize(warm.State.Banks));
    }

    private static Pending Pump(Pending pending)
    {
        NativeBattleChoco99IoState state = pending.State;
        NativeBattleEffectMemory memory = pending.Memory;
        NativeBattleFileIoState fileIo = pending.FileIo.Advance482590((current, completion) =>
        {
            NativeBattleChoco99IoCompletion complete = NativeBattleChoco99Io.Complete(state, memory, completion.Callback);
            state = complete.State;
            memory = complete.Memory;
            return current;
        });
        return pending with { State = state, Memory = memory, FileIo = fileIo };
    }
    private static ushort U16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
    private sealed record Pending(NativeBattleChoco99IoState State, NativeBattleModulePoolTask Task, NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);
}
