using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco100CameraResourcesTests
{
    [Fact]
    public void Original_eight_mode0_rows_preserve_cold_next_and_only_consumed_completions_are_required()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco100CameraResources resources = game.ReadNativeChoco100CameraResources();
        Assert.Same(resources, game.ReadNativeChoco100CameraResources());
        resources.Validate();
        Assert.Equal(0x25652E8u, NativeBattleChoco100CameraResources.StateAddress);
        NativeBattleChoco100CameraResources restoredResources = JsonSerializer.Deserialize<NativeBattleChoco100CameraResources>(JsonSerializer.Serialize(resources))!;
        restoredResources.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restoredResources));
        NativeBattleMoogleCameraState retained = NativeBattleMoogleCamera.Reset732AF0();
        for (int index = 0; index < resources.Sources.Length; index++)
        {
            NativeBattleMoogleCameraSource source = resources.Sources[index];
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()));
            Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(0x24)));
            bool completionConsumed = index is 0 or 6;
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(retained, source, [10, 0, 20, 0, 30, 0, 40, 0], 0);
            int callbacks = 0;
            while (completionConsumed ? !step.Complete && callbacks++ < 4096 : callbacks++ < 16)
            {
                NativeBattleMoogleCameraState restored = JsonSerializer.Deserialize<NativeBattleMoogleCameraState>(JsonSerializer.Serialize(step.State))!;
                NativeBattleMoogleCameraStep next = NativeBattleMoogleCamera.Advance732E00(step.State);
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(NativeBattleMoogleCamera.Advance732E00(restored)));
                step = next;
            }
            if (completionConsumed)
            {
                Assert.True(step.Complete);
            }
            retained = step.State;
        }
    }

    [Fact]
    public void Original_fourteen_phase_wrapper_retains_nine_tick_hold_request9_and_publication_before_sound()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco100CameraResources resources = game.ReadNativeChoco100CameraResources();
        NativeBattleModulePoolTask task = new(1, 0, null, NativeBattleChoco100CameraTask.Callback, new byte[0x70 - 12].ToImmutableArray());
        NativeBattleMoogleCameraState? camera = null;
        NativeBattleChoco97Context context = NativeBattleChoco97Context.Clear72A350().WriteWord(0, 10).WriteWord(2, 20).WriteWord(4, 30).WriteWord(6, 40).WriteWord(8, 50).WriteWord(10, 60).WriteWord(12, 70).WriteWord(14, 80);
        HashSet<byte> phases = [];
        List<uint> sources = [];
        uint previous = 0;
        int holdCallbacks = 0;
        int soundCalls = 0;
        for (int callback = 0; task.ReadByte(0x29) != 13 && callback < 4096; callback++)
        {
            byte phase = task.ReadByte(0x29);
            phases.Add(phase);
            ushort mode = phase <= 8 ? (ushort)8 : phase <= 10 ? (ushort)9 : (ushort)10;
            context = context.WriteWord(0x40, mode).WriteWord(0x42, mode).WriteWord(0x44, mode);
            Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(new Saved(task, camera, context)))!;
            NativeBattleChoco100CameraTaskStep step = NativeBattleChoco100CameraTask.Advance7148C0(task, camera, resources, context);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(NativeBattleChoco100CameraTask.Advance7148C0(restored.Task, restored.Camera, resources, restored.Context)));
            foreach (NativeBattleChoco97CameraPublish publication in step.Operations.OfType<NativeBattleChoco97CameraPublish>())
            {
                uint source = BinaryPrimitives.ReadUInt32LittleEndian(publication.Step.State.Data.AsSpan().Slice(0xAC));
                if (source != previous)
                {
                    sources.Add(source);
                    previous = source;
                }
            }
            if (phase == 7)
            {
                holdCallbacks++;
            }
            if (phase == 8)
            {
                Assert.Equal((ushort)9, step.Context.Word(0x44));
            }
            if (phase == 9)
            {
                Assert.Equal(3, step.Operations.Length);
                Assert.IsType<NativeBattleChoco97CameraPublish>(step.Operations[0]);
                Assert.IsType<NativeBattleChoco97CameraPublish>(step.Operations[1]);
                Assert.Equal(new NativeBattleChoco97Sound(0x1529800, 0, 128), Assert.IsType<NativeBattleChoco97Sound>(step.Operations[2]));
                soundCalls++;
            }
            if (phase == 12)
            {
                Assert.Empty(step.Operations);
            }
            task = step.Task;
            camera = step.Camera;
            context = step.Context;
        }
        Assert.Equal(Enumerable.Range(0, 13).Select(value => (byte)value).ToArray(), phases.Order().ToArray());
        Assert.Equal(resources.Sources.Select(source => source.Address).ToArray(), sources.ToArray());
        Assert.Equal(9, holdCallbacks);
        Assert.Equal(1, soundCalls);
        Assert.Equal((byte)13, task.ReadByte(0x29));
        Assert.False(NativeBattleChoco100CameraTask.Advance7148C0(task.WriteByte(0x28, 1), camera, resources, context).ReadyToComplete);
        Assert.True(NativeBattleChoco100CameraTask.Advance7148C0(task, camera, resources, context).ReadyToComplete);
    }

    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState? Camera, NativeBattleChoco97Context Context);
}
