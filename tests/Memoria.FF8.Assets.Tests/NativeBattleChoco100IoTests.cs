using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco100IoTests
{
    private const uint Buffer = 0x20DFAB8 + 0x20000;

    [Fact]
    public void Original_IO_resources_and_optional_fresh_data_export_keep_exact_file_identities()
    {
        using GameAssets game = Installed();
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO100_IO_RAW_EXPORT") is { } rawExport)
        {
            Assert.False(Directory.Exists(rawExport) && Directory.EnumerateFileSystemEntries(rawExport).Any(), "Export requires a fresh empty directory.");
            using var archive = game.OpenResourceArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> names = NativeBattleChoco100IoResourcesReader.ReadFileNames(game.ReadNativeExecutable());
            List<NativeBattleEffectFileAsset> rawFiles = [];
            foreach (short id in new short[] { 492, 495, 496, 498, 501, 502 })
            {
                NativeBattleEffectFileAsset file = Assert.Single(names, file => file.FileId == id);
                var entry = Assert.Single(archive.Entries, entry => Path.GetFileName(entry.RelativePath.Replace('\\', '/')).Equals(file.Name, StringComparison.OrdinalIgnoreCase));
                using Stream source = entry.OpenForRead();
                using MemoryStream content = new();
                source.CopyTo(content);
                rawFiles.Add(file with { Data = content.ToArray().ToImmutableArray() });
            }
            Directory.CreateDirectory(rawExport);
            foreach (NativeBattleEffectFileAsset file in rawFiles)
            {
                using FileStream stream = new(Path.Combine(rawExport, $"data-{file.FileId}.bin"), FileMode.CreateNew);
                stream.Write(file.Data.AsSpan());
            }
            using FileStream manifest = new(Path.Combine(rawExport, "manifest.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(manifest, rawFiles.Select(file => new { file.FileId, file.Name, Bytes = file.Data.Length, Sha256 = Convert.ToHexString(SHA256.HashData(file.Data.AsSpan())), First16 = Convert.ToHexString(file.Data.AsSpan().Slice(0, Math.Min(16, file.Data.Length))) }), new JsonSerializerOptions { WriteIndented = true });
        }
        NativeBattleChoco100IoResources resources = game.ReadNativeChoco100IoResources();
        Assert.Same(resources, game.ReadNativeChoco100IoResources());
        Assert.Equal(0x160, resources.Script.Data.Length);
        Assert.Equal(Enumerable.Range(489, 16).Select(id => (short)id).ToArray(), resources.Files.Select(file => file.FileId).ToArray());
        ushort[] opcodes = Enumerable.Range(0, 22).Select(row => U16(resources.Script.Data, row * 16)).ToArray();
        Assert.Equal(5, opcodes.Count(opcode => opcode == 0));
        Assert.Equal(10, opcodes.Count(opcode => opcode == 1));
        Assert.Equal(6, opcodes.Count(opcode => opcode is 2 or 3 or 5));
        Assert.Equal((ushort)0xFF, opcodes[^1]);
        Assert.DoesNotContain(opcodes, opcode => opcode is 4 or 6);
        Assert.Equal(0xECA0u, U32(resources.Script.Data, 13 * 16 + 8));
        Assert.Equal(0x5090, Assert.Single(resources.Files, file => file.FileId == 498).Data.Length);
        Assert.All(Enumerable.Range(0, 22), row => Assert.Equal(0u, U32(resources.Script.Data, row * 16 + 12)));
        string json = JsonSerializer.Serialize(resources);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleChoco100IoResources>(json)));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100IoResourcesReader.Read(new byte[128], []));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO100_IO_EXPORT") is { } export)
        {
            Assert.False(Directory.Exists(export) && Directory.EnumerateFileSystemEntries(export).Any(), "Export requires a fresh empty directory.");
            Directory.CreateDirectory(export);
            short[] ids = Enumerable.Range(0, 22).Where(row => opcodes[row] is 2 or 3 or 5).Select(row => checked((short)U16(resources.Script.Data, row * 16 + 2))).ToArray();
            Assert.Equal(new short[] { 492, 495, 496, 498, 501, 502 }, ids);
            NativeBattleEffectFileAsset[] files = ids.Select(id => resources.Files.Single(file => file.FileId == id)).ToArray();
            foreach (NativeBattleEffectFileAsset file in files)
            {
                using FileStream stream = new(Path.Combine(export, $"data-{file.FileId}.bin"), FileMode.CreateNew);
                stream.Write(file.Data.AsSpan());
            }
            using FileStream manifest = new(Path.Combine(export, "manifest.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(manifest, files.Select(file => new { file.FileId, file.Name, Bytes = file.Data.Length, Sha256 = Convert.ToHexString(SHA256.HashData(file.Data.AsSpan())) }), new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [Fact]
    public void Original_script_runs_sixteen_async_files_five_waits_and_actual_disjoint_data_writes_with_independent_bookkeeping()
    {
        using GameAssets game = Installed();
        NativeBattleChoco100IoResources resources = game.ReadNativeChoco100IoResources();
        NativeBattleChoco97IoBanks banks = new(0x19D5018, 0x22300, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x22300));
        NativeBattleChoco100IoInitialization initialized = NativeBattleChoco100Io.Initialize70EE30(resources.Script.Data, Buffer, banks);
        Pending current = new(initialized.State, new(1, 0, null, 0x70EF30, new byte[0x30 - 12].ToImmutableArray()), NativeBattleEffectMemory.FromColdProcess().BeginLoad(100), NativeBattleFileIoState.FromColdProcess());
        Dictionary<short, uint> dataDestinations = new() { [492] = 0x19EF018, [495] = 0x19E3018, [496] = 0x19E4970, [498] = 0x19E8678, [501] = 0x19D5018, [502] = 0x19DC678 };
        int waits = 0;
        int tims = 0;
        int dataFiles = 0;
        List<short> completedIds = [];
        while (U16(current.State.Script, checked((int)(current.State.CursorAddress - NativeBattleChoco100IoState.ScriptAddress))) != 0xFF)
        {
            int cursor = checked((int)(current.State.CursorAddress - NativeBattleChoco100IoState.ScriptAddress));
            ushort opcode = U16(current.State.Script, cursor);
            if (opcode == 0)
            {
                NativeBattleChoco100IoAdvance wait = NativeBattleChoco100Io.Advance70EF30(current.Task, current.State);
                Assert.Null(wait.Request);
                Assert.Equal(current.State.CursorAddress, wait.State.CursorAddress);
                NativeBattleChoco100IoRelease release = NativeBattleChoco100Io.ReleaseWait(wait.State);
                Assert.True(release.Released);
                current = current with { Task = wait.Task, State = release.State };
                waits++;
                continue;
            }
            NativeBattleChoco100IoAdvance begun = NativeBattleChoco100Io.Advance70EF30(current.Task, current.State);
            NativeBattleChoco100IoRequest request = Assert.IsType<NativeBattleChoco100IoRequest>(begun.Request);
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == request.FileId);
            Assert.Equal(opcode, request.Opcode);
            Assert.Equal(file.FileId == 498 ? 0xECA0 : file.Data.Length, request.DeclaredLength);
            if (opcode != 1)
            {
                Assert.Equal(dataDestinations[file.FileId], request.DestinationAddress);
                int range = begun.State.RangeCount - 1;
                Assert.Equal(request.DestinationAddress, U32(begun.State.Control, range * 4));
                Assert.Equal(request.DestinationAddress + (uint)request.DeclaredLength, U32(begun.State.Control, 0x80 + range * 4));
            }
            NativeBattleChoco100IoQueued copied = NativeBattleChoco100Io.CopyAndQueueFile(begun.State, current.Memory, current.FileIo, file);
            Assert.Equal((int?)file.Data.Length, copied.FileIo.Rows.Single(row => row.SchedulerCallback != 0).FileResult);
            if (opcode != 1)
            {
                Assert.Equal(file.Data.ToArray(), copied.State.Banks.Read(request.DestinationAddress, file.Data.Length));
            }
            Pending pending = new(copied.State, begun.Task, copied.Memory, copied.FileIo);
            Pending cold = JsonSerializer.Deserialize<Pending>(JsonSerializer.Serialize(pending))!;
            int ticks = pending.FileIo.Rows.Single(row => row.SchedulerCallback != 0).Countdown!.Value + 1;
            for (int tick = 0; tick < ticks; tick++)
            {
                pending = Pump(pending);
                cold = Pump(cold);
                Assert.Equal(JsonSerializer.Serialize(pending), JsonSerializer.Serialize(cold));
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
            NativeBattleChoco100IoAdvance polled = NativeBattleChoco100Io.Advance70EF30(pending.Task, pending.State);
            Assert.Equal(pending.State.CursorAddress + 16, polled.State.CursorAddress);
            current = pending with { Task = polled.Task, State = polled.State };
            completedIds.Add(file.FileId);
        }
        Assert.Equal(5, waits);
        Assert.Equal(10, tims);
        Assert.Equal(6, dataFiles);
        Assert.Equal(Enumerable.Range(489, 16).Select(id => (short)id).ToArray(), completedIds.ToArray());
        Assert.Equal((short)6, current.State.RangeCount);
        Assert.True(NativeBattleChoco100Io.Advance70EF30(current.Task, current.State).ReadyToComplete);
        foreach ((short id, uint address) in dataDestinations)
        {
            ImmutableArray<byte> original = Assert.Single(resources.Files, file => file.FileId == id).Data;
            Assert.Equal(original.ToArray(), current.State.Banks.Read(address, original.Length));
        }
        Assert.Equal(0x19F7318u, U32(current.State.Control, 0x80 + 3 * 4));
        Assert.Throws<NotSupportedException>(() => current.State.Banks.Read(0x19ED708, 1));
        Assert.Throws<NotSupportedException>(() => current.State.Banks.Read(0x19E2A04, 1));
        NativeBattleChoco100IoInitialization warm = NativeBattleChoco100Io.Initialize70EE30(current.State.Script, initialized.NextAddress, current.State.Banks);
        Assert.Equal(current.State.Script.ToArray(), warm.State.Script.ToArray());
        Assert.Equal(JsonSerializer.Serialize(current.State.Banks), JsonSerializer.Serialize(warm.State.Banks));
    }

    private static Pending Pump(Pending pending)
    {
        NativeBattleChoco100IoState state = pending.State;
        NativeBattleEffectMemory memory = pending.Memory;
        NativeBattleFileIoState fileIo = pending.FileIo.Advance482590((current, completion) =>
        {
            NativeBattleChoco100IoCompletion complete = NativeBattleChoco100Io.Complete(state, memory, completion.Callback);
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
    private sealed record Pending(NativeBattleChoco100IoState State, NativeBattleModulePoolTask Task, NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);
}
