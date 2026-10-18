using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRecover93NodeTests
{
    [Theory]
    [InlineData(93)]
    [InlineData(94)]
    public void OriginalBankFixupsPreserveRetainedOwnershipAndExactPhysicalRanges(ushort effect)
    {
        using GameAssets game = Installed();
        NativeBattleAngeloRestorationProfile profile = effect == 93 ? NativeBattleAngeloRestorationProfile.Recover93 : NativeBattleAngeloRestorationProfile.Reverse94;
        var fixture = Fixture(game, profile);
        NativeBattleRecover93NodeFrame frame = fixture.Frame;
        Assert.Equal(0x10000, frame.State.Emitters);
        Assert.Equal(0x10000 + profile.EmitterBytes, frame.State.Geometry);
        Assert.Equal(new byte[profile.EmitterBytes + profile.GeometryBytes], frame.Memory.Arena!.Read(0x10000, profile.EmitterBytes + profile.GeometryBytes));
        Assert.Equal(1u, frame.State.InitializationFlag);
        Assert.Equal(profile.SourceAddress + (profile.Effect == 93 ? 0x1B10u : 0x23F0u), U32(frame.State.Data!.Value, 4));
        Assert.Equal(profile.SourceAddress + 0x104, U32(frame.State.Data.Value, profile.Effect == 93 ? 0x44 + 12 : 0x44));
        NativeBattleRecover93NodeFrame again = NativeBattleRecover93Nodes.Initialize74EC00(frame, fixture.TaskAddress, fixture.Resource, fixture.Packets, fixture.ReadModel, _ => fixture.PoseSource);
        Assert.Equal(frame.State.Data.Value.ToArray(), again.State.Data!.Value.ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleRecover93Nodes.ClearStorage(frame, NativeBattleEffectArena.Length - 0x10000));
        Assert.Throws<NotSupportedException>(() => (frame.State with { InitializationFlag = 1, Data = null }).Validate(profile));
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.TaskAddress).WriteWord(0x29C, 2);
        Assert.Throws<NotSupportedException>(() => NativeBattleRecover93Nodes.Initialize74EC00(frame with { Effect = frame.Effect with { Pools = hierarchy.Write(fixture.TaskAddress, task).Pools } }, fixture.TaskAddress, fixture.Resource, fixture.Packets, fixture.ReadModel, _ => fixture.PoseSource));
    }

    [Theory]
    [InlineData(93)]
    [InlineData(94)]
    public void SharedLifecycleRestoresEachNextCallbackAndRoutesSpritesAndMeshesToIndependentCursors(ushort effect)
    {
        using GameAssets game = Installed();
        NativeBattleAngeloRestorationProfile profile = effect == 93 ? NativeBattleAngeloRestorationProfile.Recover93 : NativeBattleAngeloRestorationProfile.Reverse94;
        var fixture = Fixture(game, profile);
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleRecover93NodeFrame frame = fixture.Frame;
        NativeBattleActionMatrix view = Identity with { Z = 4096 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        int privateBatches = 0;
        int commonBatches = 0;
        bool complete = false;
        for (int callback = 0; callback < 500 && !complete; callback++)
        {
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess!.Initialize500400() } };
            frame = NativeBattleRecover93Nodes.BeginPass(frame, view, 0x30000);
            NativeBattleRecover93NodeFrame restored = JsonSerializer.Deserialize<NativeBattleRecover93NodeFrame>(JsonSerializer.Serialize(frame))!;
            NativeBattleRecover93NodeStep step = NativeBattleRecover93Nodes.Advance74F6E0(frame, fixture.TaskAddress, fixture.ReadModel, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
            NativeBattleRecover93NodeStep coldStep = NativeBattleRecover93Nodes.Advance74F6E0(restored, fixture.TaskAddress, fixture.ReadModel, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(coldStep));
            Assert.Equal(0x30000 + step.PrivateSprites.Sum(batch => batch.AllocationBytes), step.Frame.State.PrivateDrawCursor);
            Assert.Equal(step.CommonMeshes.Sum(batch => batch.AllocationBytes), step.Frame.Memory.PrimitiveProcess!.ByteCursor);
            Assert.Equal(0, step.Frame.Effect.Pools[1].Slots[0].ReadWord(0x34 + 0x2A));
            Assert.Equal(step.Frame.Effect.Pools[1].Slots[0].ReadWord(0x34 + 0x14), step.Frame.State.PassEmitterCount);
            Assert.Equal(step.Frame.Effect.Pools[1].Slots[0].ReadWord(0x34 + 0x16), step.Frame.State.PassGeometryCount);
            privateBatches += step.PrivateSprites.Length;
            commonBatches += step.CommonMeshes.Length;
            frame = step.Frame;
            complete = step.Complete;
        }
        Assert.True(complete, "The bounded node-only diagnostic did not drain; no whole Recover entry is admitted by this test.");
        Assert.True(privateBatches > 0);
        Assert.True(commonBatches > 0);
        Assert.Equal((ushort)0, frame.State.PassEmitterCount);
        Assert.Equal((ushort)0, frame.State.PassGeometryCount);
    }

    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static (NativeBattleRecover93NodeFrame Frame, uint TaskAddress, NativeBattleRecover93NodeResource Resource, NativeBattleActionPacketsState Packets, NativeBattlePoseSource PoseSource, Func<int, NativeBattleGraphicsModel> ReadModel) Fixture(GameAssets game, NativeBattleAngeloRestorationProfile profile)
    {
        NativeBattleRecover93NodeResource resource = profile.Effect == 93 ? game.ReadNativeRecover93NodeResource() : game.ReadNativeReverse94Resources().Nodes;
        NativeBattlePoseSource poseSource = game.ReadNativeSearchModelResources().Model.PoseSource;
        NativeBattleDecodedPose pose = NativeBattlePoseDecoder.DecodeFirst509440(poseSource.Animations.AsSpan(), poseSource.BoneCount, 0).Pose;
        NativeBattleGraphicsModel Model(int index) => new(index, 3) { ModelId4 = 0, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), GeometryWorldMatrix = Identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)), SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = pose } };
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 12, 93, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 100, 1), NativeBattleEffectTaskPool.Initialize(0x3000, 0x4000, 0x2A4, 1)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 1, 100);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteDword(0xC, 0x1D99A78).WriteWord(0x5A, 1).WriteByte(0x2C, 0).WriteByte(0x2D, 3));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(1, profile.Code(NativeBattleRecover93Nodes.ContextCallback), 0x2A4, root.Address, (_, _, _) => new(0, 3));
        hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteDword(0x30, profile.SourceAddress).WriteWord(0x29E, 30));
        NativeBattleOriginalEffectState effect = new(profile.Effect, profile.Effect - 1, new(profile.Effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() };
        NativeBattleRecover93NodeFrame frame = new(memory, effect, NativeBattleRecover93NodeState.FromColdProcess(), new(1));
        frame = NativeBattleRecover93Nodes.ClearStorage(frame, 0);
        frame = NativeBattleRecover93Nodes.Initialize74EC00(frame, child.Address, resource, packets, Model, _ => poseSource);
        return (frame, child.Address, resource, packets, poseSource, Model);
    }
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
}
