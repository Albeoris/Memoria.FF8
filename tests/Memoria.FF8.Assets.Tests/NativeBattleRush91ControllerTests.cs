using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRush91ControllerTests
{
    [Fact]
    public void FirstSamePassEmissionUsesTwentyEightRandomDrawsAndUnintegratedParticleBirths()
    {
        using GameAssets game = Installed();
        NativeBattleRush91Resources resources = game.ReadNativeRush91Resources();
        var fixture = Fixture(resources);
        NativeBattleRush91Frame frame = fixture.Frame;
        List<NativeBattleRush91Operation> operations = [];
        NativeBattleRush91Inputs inputs = Inputs(resources, game.ReadNativeBattleGraphicsRules(), fixture.Packets, operations.Add);
        bool born = false;
        for (int callback = 0; callback < 20 && !born; callback++)
        {
            NativeCrtRandomState before = frame.Random;
            bool absent = frame.Effect.Pools[3].Head is null;
            frame = NativeBattleRush91Controller.Advance759510(Reset(frame), inputs, resources);
            if (absent && frame.Effect.Pools[3].Head is not null)
            {
                NativeCrtRandomState expected = before;
                for (int draw = 0; draw < 28; draw++)
                {
                    expected = expected.Draw(out _);
                }
                Assert.Equal(expected, frame.Random);
                Assert.Equal(4, operations.Count(operation => operation is NativeBattleRush91Sprite));
                Assert.Equal(2, operations.Count(operation => operation is NativeBattleRush91Mesh));
                Assert.Equal(4, frame.Effect.Pools[5].Slots.Count(task => task.Callback != 0));
                foreach (NativeBattleModulePoolTask particle in frame.Effect.Pools[5].Slots.Where(task => task.Callback != 0))
                {
                    Assert.Equal((ushort)0, particle.ReadWord(0x1E));
                    Assert.Equal((ushort)0, particle.ReadWord(0x50));
                    Assert.Equal((ushort)1, particle.ReadWord(0x24));
                }
                Assert.All(frame.Effect.Pools[6].Slots.Where(task => task.Callback != 0), task => Assert.Equal((ushort)0, task.ReadWord(0x1E)));
                born = true;
            }
        }
        Assert.True(born);
        Assert.Throws<NotSupportedException>(() => NativeBattleRush91Controller.Initialize759370(frame.Memory with { Rush91MorphScratch = null }, fixture.Frame.Effect, fixture.Packets, resources, frame.Random));
        Assert.Throws<NotSupportedException>(() => NativeBattleRush91Controller.Initialize759370(frame.Memory with { FullscreenOverlayPause2795914 = null }, fixture.Frame.Effect, fixture.Packets, resources, frame.Random));
    }

    [Fact]
    public void OriginalLocalControllersDrainWithIndependentRestoresBeforeSeparateIoCompletion()
    {
        using GameAssets game = Installed();
        NativeBattleRush91Resources resources = game.ReadNativeRush91Resources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        var fixture = Fixture(resources);
        NativeBattleRush91Frame frame = fixture.Frame;
        List<NativeBattleRush91Operation> operations = [];
        int modelBytes = 0;
        int programBytes = 0;
        int nodePrivate = 0;
        int nodeCommon = 0;
        NativeBattleRush91Inputs inputs = Inputs(resources, rules, fixture.Packets, operations.Add, bytes => modelBytes += bytes, bytes => programBytes += bytes, (sprites, meshes) => { nodePrivate += sprites; nodeCommon += meshes; });
        NativeBattleRush91Inputs coldInputs = Inputs(resources, rules, fixture.Packets);
        Assert.Equal(new[] { 2, 4, 3, 1, 1, 80, 40, 2, 10 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 0x64, 0x58, 0x48, 0x158, 0x2A4, 0x90, 0x8C, 0x108, 0x40 }, frame.Effect.Pools.Select(pool => pool.Stride));
        HashSet<byte> phases = [];
        int restoredCallbacks = 0;
        for (int callback = 0; callback < 300 && operations.All(operation => operation is not NativeBattleRush91CommonTextureLoad); callback++)
        {
            frame = Reset(frame);
            NativeBattleRush91Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRush91Frame>(SnapshotJson(frame))!);
            NativeBattleRush91Frame next = NativeBattleRush91Controller.Advance759510(frame, inputs, resources);
            NativeBattleRush91Frame coldNext = NativeBattleRush91Controller.Advance759510(restored, coldInputs, resources);
            Assert.Equal(SnapshotJson(next), SnapshotJson(coldNext));
            restoredCallbacks++;
            if (next.Effect.Pools[3].Head is not null)
            {
                phases.Add(next.Effect.Pools[3].Slots[0].ReadByte(0x29));
            }
            frame = next;
        }
        NativeBattleRush91CommonTextureLoad load = Assert.IsType<NativeBattleRush91CommonTextureLoad>(Assert.Single(operations, operation => operation is NativeBattleRush91CommonTextureLoad));
        Assert.True(restoredCallbacks > 50);
        Assert.All(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, phase => Assert.Contains(phase, phases));
        Assert.Single(operations, operation => operation is NativeBattleRush91Contact);
        Assert.Single(operations, operation => operation is NativeBattleRush91Sound);
        Assert.Single(operations, operation => operation is NativeBattleRush91UploadModelTim);
        Assert.True(modelBytes > 0);
        Assert.True(programBytes > 0);
        Assert.True(nodePrivate > 0);
        Assert.True(nodeCommon > 0);
        Assert.Equal(1u, frame.Memory.Rush91!.Nodes.InitializationFlag);
        Assert.Equal(0xEF0, frame.Memory.Rush91.Nodes.Data!.Length);
        Assert.Equal(0x5A0, frame.Memory.Rush91MorphScratch!.Value.Length);
        Assert.Equal(0x970, frame.Memory.Rush91.ProjectedScratch!.Value.Length);
        Assert.Equal((byte)8, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.All(frame.Effect.Pools.Skip(1), pool => Assert.Null(pool.Head));
        Assert.Equal(NativeBattleRush91Controller.MainBank + 0x62, load.CompletionAddress);
        for (int callback = 0; callback < 3; callback++)
        {
            frame = NativeBattleRush91Controller.Advance759510(Reset(frame), coldInputs, resources);
            Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleRush91Controller.WriteCompletionByte(frame.Effect, load.CompletionAddress + 1, 1));
        frame = frame with { Effect = NativeBattleRush91Controller.WriteCompletionByte(frame.Effect, load.CompletionAddress, 1) };
        for (int callback = 0; callback < 3 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleRush91Controller.Advance759510(Reset(frame), coldInputs, resources);
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    private static NativeBattleRush91Inputs Inputs(NativeBattleRush91Resources resources, NativeBattleGraphicsRules rules, NativeBattleActionPacketsState packets, Action<NativeBattleRush91Operation>? observe = null, Action<int>? modelBytes = null, Action<int>? programBytes = null, Action<int, int>? nodeBytes = null)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix view = identity with { Z = 8192 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattlePoseSource actorSource = resources.Model.PoseSource;
        NativeBattleDecodedPose actorPose = NativeBattlePoseDecoder.DecodeFirst509440(actorSource.Animations.AsSpan(), actorSource.BoneCount, 0).Pose;
        NativeBattleGraphicsModel Model(int actor) => NativeBattleBodyBoneMatrices.Publish508C90(new(actor, 3) { ModelId4 = 0, Flags2 = 0, Transform = new(new(0, 0, 0), new(100, 0, 300), 77, 0, 4096), GeometryWorldMatrix = identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)), SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = actorPose } }, actorSource);
        return new(0, packets, view, Model, _ => actorSource, (frame, pointer, selector) =>
        {
            uint address = pointer - 0x30;
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
            NativeBattleEffectOwnedModelState owner = frame.Memory.Rush91!.Models.Single(model => model.TaskAddress == address);
            return NativeBattlePoseMatrices.ReadEffectOwnedBone(resources.Model.PoseSource, owner.DecodedPose!, Matrix(task), selector);
        }, (frame, task, owner) =>
        {
            NativeBattleEffectOwnedModelDraw draw = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, owner, resources.Model, view, projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!, frame.Memory.Rush91!.ProjectedScratch ?? new byte[0x970].ToImmutableArray());
            modelBytes?.Invoke(draw.Packets.AllocationBytes);
            frame = frame with { Memory = frame.Memory with { Rush91 = frame.Memory.Rush91 with { ProjectedScratch = draw.ProjectedScratch } } };
            return new(Append(frame, draw.Packets), draw.Task);
        }, (frame, task) =>
        {
            NativeBattleRush91ProgramDraw draw = NativeBattleRush91ProgramDrawing.Advance761BB0(task, resources.ProgramSource, frame.Memory.Rush91MorphScratch!.Value, view, projection, rules.SummonColors!);
            programBytes?.Invoke(draw.Packets.AllocationBytes);
            return new(Append(frame with { Memory = frame.Memory with { Rush91MorphScratch = draw.MorphScratch } }, draw.Packets), draw.Task, draw.Remaining);
        }, (frame, address) =>
        {
            NativeBattleRecover93NodeState retained = frame.Memory.Rush91!.Nodes with { PrivateDrawCursor = frame.Effect.DrawArenaOffset };
            NativeBattleRecover93NodeStep step = NativeBattleRush91Nodes.AdvanceContext(new(frame.Memory, frame.Effect, retained, frame.Random), address, Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
            nodeBytes?.Invoke(step.PrivateSprites.Sum(batch => batch.AllocationBytes), step.CommonMeshes.Sum(batch => batch.AllocationBytes));
            return new(new(step.Frame.Memory with { Rush91 = step.Frame.Memory.Rush91! with { Nodes = step.Frame.State } }, step.Frame.Effect with { DrawArenaOffset = step.Frame.State.PrivateDrawCursor }, step.Frame.Random), step.Complete);
        }, (frame, address, pause) =>
        {
            NativeBattleFullscreenOverlayStep step = NativeBattleFullscreenOverlay.Advance8DDEC0(new(frame.Memory, frame.Effect.Pools, NativeBattleLogicalOrdering.Clear()), address, pause, resources.OverlayProgram);
            return new(frame with { Memory = step.Frame.Memory, Effect = frame.Effect with { Pools = step.Frame.Pools } }, step.Result);
        }, (frame, operation) =>
        {
            observe?.Invoke(operation);
            if (operation is NativeBattleRush91Sprite sprite)
            {
                return Append(frame, NativeBattleSearchSprites.Draw7582A0(sprite.Task, resources.Sprite12.Bytes, view, projection, 0));
            }
            if (operation is NativeBattleRush91Mesh mesh)
            {
                return Append(frame, NativeBattleSearchMeshDrawing.Draw755FC0(mesh.Task, resources.ParticleMesh, view, projection, rules.SummonColors!));
            }
            return frame;
        });
    }
    private static NativeBattleRush91Frame Append(NativeBattleRush91Frame frame, NativeBattleGfGeometryBatch packets)
    {
        NativeBattleEffectPacketWrite write = NativeBattleEffectPacketArena.Append(frame.Memory, frame.Effect.DrawArenaOffset!.Value, NativeBattleLogicalOrdering.Clear(), packets);
        return frame with { Memory = write.Memory, Effect = frame.Effect with { DrawArenaOffset = write.Cursor } };
    }
    private static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task) => new(Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x70 + index * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(0x84)), unchecked((int)task.ReadDword(0x88)), unchecked((int)task.ReadDword(0x8C)), 4096);
    private static NativeBattleRush91Frame Reset(NativeBattleRush91Frame frame) => frame with { Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess!.Initialize500400() } };
    private static (NativeBattleRush91Frame Frame, NativeBattleActionPacketsState Packets) Fixture(NativeBattleRush91Resources resources)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 15, 91, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(91, 90, new(91, 0x759370, 0x759350), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x2598F28, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() };
        return (NativeBattleRush91Controller.Initialize759370(memory, effect, packets, resources, new(1)), packets);
    }
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
