using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99CameraResourcesTests
{
    [Fact]
    public void Original_seven_rows_preserve_cold_next_equality_and_completion_where_the_caller_consumes_it()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco99CameraResources resources = game.ReadNativeChoco99CameraResources();
        Assert.Same(resources, game.ReadNativeChoco99CameraResources());
        resources.Validate();
        Assert.Equal(0x256D600u, NativeBattleChoco99CameraResources.StateAddress);
        Assert.Equal(new[] { 0, 0, 0, 1, 0, 0, 0 }, resources.Sources.Select(source => (int)BinaryPrimitives.ReadInt16LittleEndian(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(0x24))).ToArray());
        NativeBattleChoco99CameraResources restoredResources = NativeBattleSnapshotJson.Deserialize<NativeBattleChoco99CameraResources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restoredResources.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources), NativeBattleSnapshotJson.Serialize(restoredResources));
        NativeBattleMoogleCameraState retained = NativeBattleMoogleCamera.Reset732AF0();
        for (int index = 0; index < resources.Sources.Length; index++)
        {
            NativeBattleMoogleCameraSource source = resources.Sources[index];
            bool completionConsumed = index is 0 or 5;
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(retained, source, [10, 0, 20, 0, 30, 0, 40, 0], 0);
            int callbacks = 0;
            while (completionConsumed ? !step.Complete && callbacks++ < 4096 : callbacks++ < 16)
            {
                NativeBattleMoogleCameraState restored = NativeBattleSnapshotJson.Deserialize<NativeBattleMoogleCameraState>(NativeBattleSnapshotJson.Serialize(step.State))!;
                NativeBattleMoogleCameraStep next = NativeBattleMoogleCamera.Advance732E00(step.State);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(NativeBattleMoogleCamera.Advance732E00(restored)));
                step = next;
            }
            if (completionConsumed)
            {
                Assert.True(step.Complete);
            }
            Assert.Equal(source.Address, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(step.State).Data.AsSpan().Slice(0xAC)));
            retained = step.State;
        }
    }

    [Fact]
    public void Original_ten_phase_wrapper_preserves_all_source_transitions_and_manual_double_publications()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco99CameraResources resources = game.ReadNativeChoco99CameraResources();
        NativeBattleModulePoolTask task = new(1, 0, null, NativeBattleChoco99CameraTask.Callback, new byte[0xB0 - 12].ToImmutableArray());
        NativeBattleMoogleCameraState? camera = null;
        HashSet<byte> phases = [];
        List<uint> sources = [];
        uint previous = 0;
        bool manualNoAdvance = false;
        bool phase8Double = false;
        for (int callback = 0; task.ReadByte(0x29) != 9 && callback < 4096; callback++)
        {
            byte phase = task.ReadByte(0x29);
            phases.Add(phase);
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(new Saved(task, camera)))!;
            NativeBattleChoco99CameraTaskStep next = Advance(task, camera);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(Advance(restored.Task, restored.Camera)));
            if (phase == 6)
            {
                Assert.Equal(2, next.Publications.Length);
                Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(NativeBattleStorageSnapshots.Capture(camera!).Data.AsSpan().Slice(0xE2)), BinaryPrimitives.ReadInt16LittleEndian(NativeBattleStorageSnapshots.Capture(next.Publications[0].State).Data.AsSpan().Slice(0xE2)));
                manualNoAdvance = true;
            }
            if (phase == 8)
            {
                Assert.Equal(2, next.Publications.Length);
                Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(NativeBattleStorageSnapshots.Capture(next.Publications[0].State).Data.AsSpan().Slice(0xE2)), BinaryPrimitives.ReadInt16LittleEndian(NativeBattleStorageSnapshots.Capture(next.Publications[1].State).Data.AsSpan().Slice(0xE2)));
                phase8Double = true;
            }
            foreach (NativeBattleMoogleCameraStep publication in next.Publications)
            {
                uint source = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(publication.State).Data.AsSpan().Slice(0xAC));
                if (source != previous)
                {
                    sources.Add(source);
                    previous = source;
                }
            }
            task = next.Task;
            camera = next.Camera;
        }
        Assert.Equal(Enumerable.Range(0, 9).Select(value => (byte)value).ToArray(), phases.Order().ToArray());
        Assert.Equal(resources.Sources.Select(source => source.Address).ToArray(), sources.ToArray());
        Assert.True(manualNoAdvance);
        Assert.True(phase8Double);
        Assert.Equal((byte)9, task.ReadByte(0x29));
        Assert.False(Advance(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(task)).WriteByte(0x28, 1), camera).ReadyToComplete);
        Assert.True(Advance(task, camera).ReadyToComplete);

        NativeBattleChoco99CameraTaskStep Advance(NativeBattleModulePoolTask currentTask, NativeBattleMoogleCameraState? currentCamera) => NativeBattleChoco99CameraTask.Advance71DC40(currentTask, currentCamera, resources, 11, [10, 0, 20, 0, 30, 0, 40, 0], [50, 0, 60, 0, 70, 0, 80, 0]);
    }

    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState? Camera);
}
