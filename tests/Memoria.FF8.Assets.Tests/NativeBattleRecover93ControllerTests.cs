using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRecover93ControllerTests
{
    [Theory]
    [InlineData(93)]
    [InlineData(94)]
    public void OriginalResourcesDrainAllLocalChildrenAndWaitForActualAuxiliaryCompletion(ushort effect)
    {
        using GameAssets game = Installed();
        NativeBattleAngeloRestorationProfile profile = effect == 93 ? NativeBattleAngeloRestorationProfile.Recover93 : NativeBattleAngeloRestorationProfile.Reverse94;
        NativeBattleRecover93Resources resources = effect == 93 ? game.ReadNativeRecover93Resources() : game.ReadNativeReverse94Resources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        var fixture = Fixture(resources, profile);
        NativeBattleRecover93Frame frame = fixture.Frame;
        List<NativeBattleRecover93Operation> operations = [];
        int programFrames = 0;
        int modelBytes = 0;
        NativeBattleRecover93Inputs inputs = Inputs(resources, rules, fixture.Packets, profile, operation => operations.Add(operation), () => programFrames++, bytes => modelBytes += bytes);
        Assert.Equal(new[] { 2, 4, 3, 1, 1, 20, 8, 2, 10 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 0x64, 0x58, 0x48, 0x140, 0x2A4, 0xB0, 0x58, 0x17C, 0x40 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Null(profile.Read(frame.Memory)!.DogControl);
        HashSet<int> observedModelStages = [];
        bool observedSamePassModelInitialization = false;
        for (int callback = 0; callback < 400 && operations.All(operation => operation is not NativeBattleRecover93CommonTextureLoad); callback++)
        {
            frame = ResetFramePackets(frame);
            bool modelWasAbsent = frame.Effect.Pools[3].Head is null;
            if (frame.Effect.Pools[3].Head is not null)
            {
                observedModelStages.Add(frame.Effect.Pools[3].Slots[0].ReadByte(0x29));
            }
            frame = NativeBattleRecover93Controller.Advance74E520(frame, inputs, resources);
            if (modelWasAbsent && frame.Effect.Pools[3].Head is not null)
            {
                NativeBattleModulePoolTask model = frame.Effect.Pools[3].Slots[0];
                Assert.Equal((byte)1, model.ReadByte(0x29));
                Assert.Equal((ushort)1, model.ReadWord(0x24));
                Assert.Equal(unchecked((ushort)-256), model.ReadWord(0x128));
                Assert.NotNull(Assert.Single(profile.Read(frame.Memory)!.Models).DecodedPose);
                Assert.Single(operations, operation => operation is NativeBattleRecover93Voice voice && voice.Descriptor == resources.VoiceAppearance);
                observedSamePassModelInitialization = true;
            }
        }
        NativeBattleRecover93CommonTextureLoad load = Assert.IsType<NativeBattleRecover93CommonTextureLoad>(Assert.Single(operations, operation => operation is NativeBattleRecover93CommonTextureLoad));
        Assert.Equal(profile.MainBank + 0x62, load.CompletionAddress);
        Assert.Equal((byte)8, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools.Skip(1), pool => Assert.Null(pool.Head));
        Assert.All(new[] { 1, 2, 3 }, phase => Assert.Contains(phase, observedModelStages));
        Assert.True(observedSamePassModelInitialization);
        Assert.Single(operations, operation => operation is NativeBattleRecover93Contact);
        Assert.Single(operations, operation => operation is NativeBattleRecover93UploadModelTim);
        Assert.Equal(2, operations.Count(operation => operation is NativeBattleRecover93Sound));
        Assert.Equal(2, operations.Count(operation => operation is NativeBattleRecover93Voice));
        Assert.Equal(12, programFrames);
        Assert.True(modelBytes > 0);
        Assert.Equal(1u, profile.Read(frame.Memory)!.Nodes.InitializationFlag);
        Assert.Equal((ushort)0, profile.Read(frame.Memory)!.Nodes.PassEmitterCount);
        Assert.Equal((ushort)0, profile.Read(frame.Memory)!.Nodes.PassGeometryCount);
        Assert.NotEqual(new NativeCrtRandomState(1), frame.Random);
        Assert.Single(operations, operation => operation is NativeBattleRecover93StageClearRgb);
        Assert.Equal(new short[] { 0, 256, 512, 768, 1024, 1280, 1536, 1280, 1024, 768, 512, 256, 0 }, operations.OfType<NativeBattleRecover93StageDepth>().Select(operation => operation.Value));
        for (int callback = 0; callback < 4; callback++)
        {
            frame = NativeBattleRecover93Controller.Advance74E520(ResetFramePackets(frame), inputs, resources);
            Assert.Equal((byte)8, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleRecover93Controller.WriteCompletionByte(frame.Effect, load.CompletionAddress + 1, 1));
        frame = frame with { Effect = NativeBattleRecover93Controller.WriteCompletionByte(frame.Effect, load.CompletionAddress, 1) };
        for (int callback = 0; callback < 3 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleRecover93Controller.Advance74E520(ResetFramePackets(frame), inputs, resources);
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Theory]
    [InlineData(93)]
    [InlineData(94)]
    public void IndependentRestorePreservesModelNodeBankRandomAndStageLifecycle(ushort effect)
    {
        using GameAssets game = Installed();
        NativeBattleAngeloRestorationProfile profile = effect == 93 ? NativeBattleAngeloRestorationProfile.Recover93 : NativeBattleAngeloRestorationProfile.Reverse94;
        NativeBattleRecover93Resources resources = effect == 93 ? game.ReadNativeRecover93Resources() : game.ReadNativeReverse94Resources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        var fixture = Fixture(resources, profile);
        NativeBattleRecover93Frame frame = fixture.Frame;
        NativeBattleRecover93Inputs inputs = Inputs(resources, rules, fixture.Packets, profile);
        int boundaries = 0;
        for (int callback = 0; callback < 160; callback++)
        {
            frame = ResetFramePackets(frame);
            NativeBattleRecover93Frame restored = JsonSerializer.Deserialize<NativeBattleRecover93Frame>(JsonSerializer.Serialize(frame))!;
            NativeBattleRecover93Frame next = NativeBattleRecover93Controller.Advance74E520(frame, inputs, resources);
            NativeBattleRecover93Frame coldNext = NativeBattleRecover93Controller.Advance74E520(restored, inputs, resources);
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(coldNext));
            if (callback is 20 or 50 or 80 or 110)
            {
                boundaries++;
                Assert.NotSame(frame.Effect.Pools[3].Slots[0], restored.Effect.Pools[3].Slots[0]);
            }
            frame = next;
        }
        Assert.Equal(4, boundaries);
        Assert.Equal((byte)8, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.NotNull(profile.Read(frame.Memory)!.PublishedModelMatrix);
        Assert.Equal(0x970, profile.Read(frame.Memory)!.ProjectedScratch!.Value.Length);
        Assert.Equal(profile.SourceLength, profile.Read(frame.Memory)!.Nodes.Data!.Value.Length);
    }

    [Fact]
    public void SwitchingModulesPreservesIndependentMutableBanksAndExisting93JsonNames()
    {
        using GameAssets game = Installed();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleRecover93Resources recoverResources = game.ReadNativeRecover93Resources();
        NativeBattleRecover93Resources reverseResources = game.ReadNativeReverse94Resources();
        NativeBattleAngeloRestorationProfile recover = NativeBattleAngeloRestorationProfile.Recover93;
        NativeBattleAngeloRestorationProfile reverse = NativeBattleAngeloRestorationProfile.Reverse94;
        var first = Fixture(recoverResources, recover);
        NativeBattleRecover93Frame frame = first.Frame;
        NativeBattleRecover93Inputs firstInputs = Inputs(recoverResources, rules, first.Packets, recover);
        for (int callback = 0; callback < 100; callback++)
        {
            frame = NativeBattleRecover93Controller.Advance74E520(ResetFramePackets(frame), firstInputs, recoverResources);
        }
        Assert.Equal(1u, recover.Read(frame.Memory)!.Nodes.InitializationFlag);
        string retained = JsonSerializer.Serialize(recover.Read(frame.Memory));
        string originalJson = JsonSerializer.Serialize(frame);
        Assert.Contains("\"Recover93\":", originalJson);
        Assert.DoesNotContain("\"Reverse94\":", originalJson);
        var second = Fixture(reverseResources, reverse);
        frame = NativeBattleRecover93Controller.Initialize74E390(frame.Memory, second.Frame.Effect, second.Packets, reverseResources, frame.Random);
        Assert.Equal(0u, reverse.Read(frame.Memory)!.Nodes.InitializationFlag);
        Assert.Null(reverse.Read(frame.Memory)!.Nodes.Data);
        NativeBattleRecover93Inputs secondInputs = Inputs(reverseResources, rules, second.Packets, reverse);
        for (int callback = 0; callback < 140; callback++)
        {
            frame = NativeBattleRecover93Controller.Advance74E520(ResetFramePackets(frame), secondInputs, reverseResources);
        }
        Assert.Equal(retained, JsonSerializer.Serialize(recover.Read(frame.Memory)));
        Assert.Equal(1u, reverse.Read(frame.Memory)!.Nodes.InitializationFlag);
        Assert.Equal(0x2C00, reverse.Read(frame.Memory)!.Nodes.Data!.Value.Length);
        Assert.Equal(0x10CA8, reverse.Read(frame.Memory)!.Nodes.Geometry);
        Assert.Throws<InvalidDataException>(() => reverse.Read(frame.Memory)!.Nodes.Validate(recover));
        Assert.Throws<InvalidDataException>(() => reverse.Global(0x1D8E054));
        Assert.Throws<InvalidDataException>(() => reverse.Code(0x8DD0F0));
        Assert.Throws<InvalidDataException>(() => NativeBattleAngeloRestorationProfile.ForBinding(new(94, recover.Entry, recover.Initializer)));
    }

    private static NativeBattleRecover93Frame ResetFramePackets(NativeBattleRecover93Frame frame) => frame with { Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess!.Initialize500400() } };

    private static NativeBattleRecover93Inputs Inputs(NativeBattleRecover93Resources resources, NativeBattleGraphicsRules rules, NativeBattleActionPacketsState packets, NativeBattleAngeloRestorationProfile profile, Action<NativeBattleRecover93Operation>? observe = null, Action? program = null, Action<int>? modelPackets = null)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix view = identity with { Z = 8192 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattlePoseSource actorSource = resources.Model.PoseSource;
        NativeBattleDecodedPose actorPose = NativeBattlePoseDecoder.DecodeFirst509440(actorSource.Animations.AsSpan(), actorSource.BoneCount, 0).Pose;
        return new(packets, view, actor => new(actor, 3) { ModelId4 = 0, Flags2 = 0, Transform = new(new(0, 0, 0), new(100, 0, 300), 77, 0, 4096), GeometryWorldMatrix = identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)), SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = actorPose } }, _ => actorSource, _ => World(), (frame, rawModel, selector) =>
        {
            uint address = rawModel - 0x30;
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
            NativeBattleEffectOwnedModelState state = profile.Read(frame.Memory)!.Models.Single(model => model.TaskAddress == address);
            return NativeBattlePoseMatrices.ReadEffectOwnedBone(resources.Model.PoseSource, state.DecodedPose!, Matrix(task), selector);
        }, (frame, task, state) =>
        {
            NativeBattleEffectOwnedModelDraw draw = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, state, resources.Model, view, projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!, profile.Read(frame.Memory)!.ProjectedScratch ?? new byte[0x970].ToImmutableArray());
            modelPackets?.Invoke(draw.Packets.AllocationBytes);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, frame.Effect.DrawArenaOffset!.Value, NativeBattleLogicalOrdering.Clear(), draw.Packets);
            return new(frame with { Memory = profile.Write(written.Memory, profile.Read(frame.Memory)! with { ProjectedScratch = draw.ProjectedScratch }), Effect = frame.Effect with { DrawArenaOffset = written.Cursor } }, draw.Task);
        }, (frame, task) =>
        {
            NativeBattleSearchProgramDraw draw = NativeBattleSearchProgramDrawing.Advance758D70(task, resources.ProgramSource, view, projection, rules.SummonColors!);
            program?.Invoke();
            return new(frame, draw.Task, draw.Remaining);
        }, (frame, address) =>
        {
            NativeBattleRecover93NodeState retained = profile.Read(frame.Memory)!.Nodes with { PrivateDrawCursor = frame.Effect.DrawArenaOffset };
            NativeBattleRecover93NodeStep step = NativeBattleRecover93Nodes.Advance74F6E0(new(frame.Memory, frame.Effect, retained, frame.Random), address, actor => new(actor, 3) { ModelId4 = 0, Transform = new(new(0, 0, 0), new(100, 0, 300), 77, 0, 4096), GeometryWorldMatrix = identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)), SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = actorPose } }, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
            return new(new(profile.Write(step.Frame.Memory, profile.Read(step.Frame.Memory)! with { Nodes = step.Frame.State }), step.Frame.Effect with { DrawArenaOffset = step.Frame.State.PrivateDrawCursor }, step.Frame.Random), step.Complete);
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
    private static (NativeBattleRecover93Frame Frame, NativeBattleActionPacketsState Packets) Fixture(NativeBattleRecover93Resources resources, NativeBattleAngeloRestorationProfile profile)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 13, profile.Effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(profile.Effect, profile.Effect - 1, new(profile.Effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.Global(0x2592548), 0)] };
        return (NativeBattleRecover93Controller.Initialize74E390(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() }, effect, packets, resources, new(1)), packets);
    }
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
}
