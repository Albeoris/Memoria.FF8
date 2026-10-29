using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSearchControllerTests
{
    [Fact]
    public void OriginalLocalResourcesDriveHierarchy286RandomDrawsAndSeparateActualIoCompletion()
    {
        using GameAssets game = Installed();
        NativeBattleSearchResources resources = game.ReadNativeSearchResources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        var fixture = Fixture(resources);
        NativeBattleSearchFrame frame = fixture.Frame;
        List<NativeBattleSearchOperation> operations = [];
        int programFrames = 0;
        int modelPackets = 0;
        NativeBattleSearchInputs inputs = Inputs(resources, rules, fixture.Packets, operation => operations.Add(operation), () => programFrames++, bytes => modelPackets += bytes);
        Assert.Equal(new[] { 2, 4, 3, 1, 40, 40, 2, 10 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 0x64, 0x58, 0x48, 0x154, 0x90, 0x8C, 0x17C, 0x40 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Null(frame.Memory.Search!.DogControl);
        Assert.Null(frame.Memory.Search.PublishedModelMatrix);
        for (int callback = 0; callback < 300 && operations.All(operation => operation is not NativeBattleSearchCommonTextureLoad); callback++)
        {
            frame = NativeBattleSearchController.Advance7552B0(frame, inputs, resources);
        }
        NativeBattleSearchCommonTextureLoad load = Assert.IsType<NativeBattleSearchCommonTextureLoad>(Assert.Single(operations, operation => operation is NativeBattleSearchCommonTextureLoad));
        Assert.Equal(NativeBattleSearchController.MainBank + 0x62, load.CompletionAddress);
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((byte)8, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.All(frame.Effect.Pools.Skip(1), pool => Assert.Null(pool.Head));
        Assert.Equal(208, operations.Count(operation => operation is NativeBattleSearchSprite));
        Assert.Equal(364, operations.Count(operation => operation is NativeBattleSearchMesh));
        Assert.Single(operations, operation => operation is NativeBattleSearchContact);
        Assert.Single(operations, operation => operation is NativeBattleSearchBindTargetResource);
        Assert.Equal(new byte[] { 1, 2 }, operations.OfType<NativeBattleSearchSelectActorSequence>().Select(operation => operation.Sequence));
        Assert.Equal(3, operations.Count(operation => operation is NativeBattleSearchSound));
        Assert.Equal(2, operations.Count(operation => operation is NativeBattleSearchVoice));
        Assert.Equal(12, programFrames);
        Assert.True(modelPackets > 0);
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Search!.DogControl!.Value.AsSpan().Slice(0x48)));
        Assert.Equal((short)77, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Search.DogControl.Value.AsSpan().Slice(14)));
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 286; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        for (int callback = 0; callback < 4; callback++)
        {
            frame = NativeBattleSearchController.Advance7552B0(frame, inputs, resources);
            Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
            Assert.Equal((byte)8, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleSearchController.WriteCompletionByte(frame.Effect, load.CompletionAddress + 1, 1));
        frame = frame with { Effect = NativeBattleSearchController.WriteCompletionByte(frame.Effect, load.CompletionAddress, 1) };
        for (int callback = 0; callback < 3 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleSearchController.Advance7552B0(frame, inputs, resources);
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void ModelHierarchyAndProgramCacheRestoreExactlyAndPauseBitsDoNotGateLocalCallbacks()
    {
        using GameAssets game = Installed();
        NativeBattleSearchResources resources = game.ReadNativeSearchResources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        var fixture = Fixture(resources);
        NativeBattleSearchFrame normal = fixture.Frame;
        NativeBattleSearchFrame flagged = NativeBattleSnapshotJson.Deserialize<NativeBattleSearchFrame>(NativeBattleSnapshotJson.Serialize(fixture.Frame))!;
        NativeBattleSearchInputs inputs = Inputs(resources, rules, fixture.Packets);
        for (int callback = 0; callback < 200; callback++)
        {
            normal = NativeBattleSearchController.Advance7552B0(normal, inputs, resources);
            flagged = NativeBattleSearchController.Advance7552B0(flagged, inputs with { GraphicsFlags = 0x201 }, resources);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(normal), NativeBattleSnapshotJson.Serialize(flagged));
            if (callback is 25 or 80 or 115)
            {
                flagged = NativeBattleSnapshotJson.Deserialize<NativeBattleSearchFrame>(NativeBattleSnapshotJson.Serialize(normal))!;
                Assert.NotSame(normal.Effect.Pools[3].Slots[0], flagged.Effect.Pools[3].Slots[0]);
            }
        }
        Assert.Equal((byte)8, normal.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.NotNull(normal.Memory.Search!.PublishedModelMatrix);
        Assert.Equal(0x970, normal.Memory.Search.ProjectedScratch!.Value.Length);
    }

    private static NativeBattleSearchInputs Inputs(NativeBattleSearchResources resources, NativeBattleGraphicsRules rules, NativeBattleActionPacketsState packets, Action<NativeBattleSearchOperation>? observe = null, Action? program = null, Action<int>? modelPackets = null)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix view = identity with { Z = 8192 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattlePoseSource actorSource = resources.ModelProfile.Model.PoseSource;
        NativeBattleDecodedPose actorPose = NativeBattlePoseDecoder.DecodeFirst509440(actorSource.Animations.AsSpan(), actorSource.BoneCount, 0).Pose;
        return new(0, packets, view, actor => NativeBattleBodyBoneMatrices.Publish508C90(new(actor, 3) { ModelId4 = 0, Flags2 = 0, Transform = new(new(0, 0, 0), new(100, 0, 300), 77, 0, 4096), GeometryWorldMatrix = identity, SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = actorPose } }, actorSource), _ => actorSource, _ => World(), (frame, rawModel, selector) =>
        {
            uint address = rawModel - 0x30;
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
            NativeBattleEffectOwnedModelState state = frame.Memory.Search!.Models.Single(model => model.TaskAddress == address);
            return NativeBattlePoseMatrices.ReadEffectOwnedBone(resources.ModelProfile.Model.PoseSource, state.DecodedPose!, Matrix(task), selector);
        }, (frame, task, state) =>
        {
            NativeBattleEffectOwnedModelDraw draw = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, state, resources.ModelProfile.Model, view, projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!, frame.Memory.Search!.ProjectedScratch ?? new byte[0x970].ToImmutableArray());
            modelPackets?.Invoke(draw.Packets.AllocationBytes);
            return new(frame with { Memory = frame.Memory with { Search = frame.Memory.Search with { ProjectedScratch = draw.ProjectedScratch } } }, draw.Task);
        }, (frame, task) =>
        {
            NativeBattleSearchProgramDraw draw = NativeBattleSearchProgramDrawing.Advance758D70(task, resources.ProgramSource, view, projection, rules.SummonColors!);
            program?.Invoke();
            return new(frame, draw.Task, draw.Remaining);
        }, (frame, operation) =>
        {
            observe?.Invoke(operation);
            return frame;
        });
    }

    private static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task) => new(Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x70 + index * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(0x84)), unchecked((int)task.ReadDword(0x88)), unchecked((int)task.ReadDword(0x8C)), 4096);
    private static ImmutableArray<byte> World()
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), 300);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), 77);
        return bytes.ToImmutableArray();
    }
    private static (NativeBattleSearchFrame Frame, NativeBattleActionPacketsState Packets) Fixture(NativeBattleSearchResources resources)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 14, 92, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(92, 91, new(92, 0x755130, 0x755110), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x2594D98, 0)] };
        return (NativeBattleSearchController.Initialize755130(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, packets, resources, new(1)), packets);
    }
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
}
