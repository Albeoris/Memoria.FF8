using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco98CameraResourcesTests
{
    [Fact]
    public void All_five_original_rows_and_both_target_curves_complete_with_exact_independent_continuation()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco98CameraResources resources = game.ReadNativeChoco98CameraResources();
        Assert.Same(resources, game.ReadNativeChoco98CameraResources());
        resources.Validate();
        Assert.Equal(36, resources.Curve.Values.Length);
        NativeBattleChoco98CameraResources restoredResources = NativeBattleSnapshotJson.Deserialize<NativeBattleChoco98CameraResources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restoredResources.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources), NativeBattleSnapshotJson.Serialize(restoredResources));
        NativeBattleMoogleCameraState retained = NativeBattleMoogleCamera.Reset732AF0();
        for (int index = 0; index < 5; index++)
        {
            ImmutableArray<byte> anchor = index < 3 ? [10, 0, 20, 0, 30, 0, 40, 0] : [50, 0, 60, 0, 70, 0, 80, 0];
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(retained, resources.Sources[index], anchor, 0, resources.Curve);
            int callbacks = 0;
            while (!step.Complete && callbacks++ < 4096)
            {
                NativeBattleMoogleCameraState restored = NativeBattleSnapshotJson.Deserialize<NativeBattleMoogleCameraState>(NativeBattleSnapshotJson.Serialize(step.State))!;
                NativeBattleMoogleCameraStep next = NativeBattleMoogleCamera.Advance732E00(step.State, resources.Curve);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(NativeBattleMoogleCamera.Advance732E00(restored, resources.Curve)));
                step = next;
            }
            Assert.True(step.Complete);
            Assert.Equal(anchor.ToArray(), NativeBattleStorageSnapshots.Capture(step.State).Data.AsSpan().Slice(0xA4, 8).ToArray());
            Assert.Equal(index < 3 ? 0u : resources.Curve.Address + 70, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(step.State).Data.AsSpan().Slice(0x50)));
            retained = step.State;
        }
    }

    [Fact]
    public void Original_nine_phase_wrapper_retains_ordered_curve_publications_and_actual_child_hold()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco98CameraResources resources = game.ReadNativeChoco98CameraResources();
        NativeBattleModulePoolTask task = new(1, 0, null, NativeBattleChoco98CameraTask.Callback, new byte[0x70 - 12].ToImmutableArray());
        NativeBattleMoogleCameraState? camera = null;
        HashSet<byte> phases = [];
        List<uint> sourceTransitions = [];
        uint previous = 0;
        int doublePublications = 0;
        for (int callback = 0; task.ReadByte(0x29) != 8 && callback < 4096; callback++)
        {
            phases.Add(task.ReadByte(0x29));
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(new Saved(task, camera)))!;
            NativeBattleChoco98CameraTaskStep next = Advance(task, camera);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(Advance(restored.Task, restored.Camera)));
            foreach (NativeBattleMoogleCameraStep publication in next.Publications)
            {
                uint source = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(publication.State).Data.AsSpan().Slice(0xAC));
                if (source != previous)
                {
                    sourceTransitions.Add(source);
                    previous = source;
                }
            }
            doublePublications += next.Publications.Length == 2 ? 1 : 0;
            task = next.Task;
            camera = next.Camera;
        }
        Assert.Equal(Enumerable.Range(0, 8).Select(value => (byte)value).ToArray(), phases.Order().ToArray());
        Assert.Equal(resources.Sources.Select(source => source.Address).ToArray(), sourceTransitions.ToArray());
        Assert.Equal(3, doublePublications);
        Assert.Equal((byte)8, task.ReadByte(0x29));
        NativeBattleChoco98CameraTaskStep held = Advance(task.WriteByte(0x28, 1), camera);
        Assert.False(held.ReadyToComplete);
        Assert.Empty(held.Publications);
        Assert.True(Advance(task, camera).ReadyToComplete);

        NativeBattleChoco98CameraTaskStep Advance(NativeBattleModulePoolTask currentTask, NativeBattleMoogleCameraState? currentCamera) => NativeBattleChoco98CameraTask.Advance7276F0(currentTask, currentCamera, resources, currentTask.ReadByte(0x29) < 3 ? (short)2 : (short)6, [10, 0, 20, 0, 30, 0, 40, 0], [50, 0, 60, 0, 70, 0, 80, 0]);
    }

    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState? Camera);
}
