using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMoogle96NodesTests
{
    [Fact]
    public void BothOriginalBanksShareWorkWhileFirstUseAndMutableBytesStayIndependentAcrossRestore()
    {
        using GameAssets game = Installed();
        NativeBattleMoogle96Resources resources = game.ReadNativeMoogle96Resources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix view = identity with { Z = 16384 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattlePoseSource poseSource = resources.Model.PoseSource;
        NativeBattleDecodedPose pose = NativeBattlePoseDecoder.DecodeFirst509440(poseSource.Animations.AsSpan(), poseSource.BoneCount, 0).Pose;
        NativeBattleGraphicsModel Model(int actor) => new(actor, 3) { ModelId4 = 0, Transform = new(new(0, 0, 0), new((short)(actor * 100), 0, 300), 0, 0, 4096), GeometryWorldMatrix = identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)), SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = pose } };
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 1, 96, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x64, 1), NativeBattleEffectTaskPool.Initialize(0x257E258, 0x257DA68, 0x2A4, 3)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 1, 0x64);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteDword(0xC, 0x1D99A78).WriteWord(0x5A, 1).WriteByte(0x2C, 0).WriteByte(0x2D, 3));
        List<uint> addresses = [];
        foreach (uint source in new uint[] { 0x169227C, 0x1696DE8 })
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(1, NativeBattleMoogle96Nodes.Callback, 0x2A4, root.Address, (_, _, _) => new(0, 3));
            hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteDword(0x30, source).WriteWord(0x29C, 2).WriteWord(0x29E, 45));
            addresses.Add(appended.Address);
        }
        NativeBattleOriginalEffectState effect = new(96, 95, new(96, 0x731DE0, 0x731DC0), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x1FFF, new byte[] { 0xA5 }).Write(0x13760, new byte[] { 0xB6 });
        NativeBattleMoogle96NodeFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() }, effect, NativeBattleMoogle96NodeState.FromColdProcess(), new(1));
        frame = NativeBattleMoogle96Nodes.ClearStorage(frame, 0);
        Assert.Equal(0x2000, frame.State.Work.Emitters);
        Assert.Equal(0x30E0, frame.State.Work.Geometry);
        Assert.Equal(new byte[0x11760], frame.Memory.Arena!.Read(0x2000, 0x11760));
        Assert.Equal(0xA5, frame.Memory.Arena.Read(0x1FFF, 1)[0]);
        Assert.Equal(0xB6, frame.Memory.Arena.Read(0x13760, 1)[0]);
        NativeBattleEffectNodeDogOrigins dog = new(new(-200, -400, 100), new(200, -768, 1000));
        string? firstBank = null;
        for (int sourceIndex = 0; sourceIndex < 2; sourceIndex++)
        {
            uint source = sourceIndex == 0 ? 0x169227Cu : 0x1696DE8u;
            NativeBattleMoogle96NodeResource resource = new(source, sourceIndex == 0 ? resources.NodesA.Data : resources.NodesB.Data);
            frame = NativeBattleMoogle96Nodes.InitializeContext(frame, addresses[sourceIndex], resource, packets, Model, _ => poseSource, dog);
            Assert.Equal(1u, frame.State.Banks[sourceIndex].InitializationFlag);
            Assert.Null(frame.State.Work.Data);
            Assert.Equal(0u, frame.State.Work.InitializationFlag);
            if (sourceIndex == 0)
            {
                Assert.Equal(0u, frame.State.Banks[1].InitializationFlag);
                Assert.Null(frame.State.Banks[1].Data);
                Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96Nodes.AdvanceContext(frame, addresses[1], Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!));
            }
            bool complete = false;
            int privatePackets = 0;
            int nativeMeshPackets = 0;
            for (int callback = 0; callback < 600 && !complete; callback++)
            {
                frame = frame with { Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess!.Initialize500400() } };
                frame = NativeBattleMoogle96Nodes.BeginPass(frame, view, 0x20000);
                NativeBattleMoogle96NodeFrame restored = JsonSerializer.Deserialize<NativeBattleMoogle96NodeFrame>(JsonSerializer.Serialize(frame))!;
                NativeBattleMoogle96NodeStep step = NativeBattleMoogle96Nodes.AdvanceContext(frame, addresses[sourceIndex], Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
                NativeBattleMoogle96NodeStep coldStep = NativeBattleMoogle96Nodes.AdvanceContext(restored, addresses[sourceIndex], Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
                Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(coldStep));
                Assert.Null(step.Frame.State.Work.Data);
                Assert.Equal(0u, step.Frame.State.Work.InitializationFlag);
                Assert.Equal(0x20000 + step.PrivateSprites.Sum(batch => batch.AllocationBytes), step.Frame.State.Work.PrivateDrawCursor);
                Assert.Equal(step.CommonMeshes.Sum(batch => batch.AllocationBytes), step.Frame.Memory.PrimitiveProcess!.ByteCursor);
                privatePackets += step.PrivateSprites.Sum(batch => batch.Attempts.Count(attempt => attempt.Primitive is not null));
                nativeMeshPackets += step.CommonMeshes.Sum(batch => batch.Attempts.Count(attempt => attempt.Primitive is { } primitive && primitive.Words.Length == (sourceIndex == 0 ? 8 : 10)));
                frame = step.Frame;
                complete = step.Complete;
            }
            Assert.True(complete, "The bounded original node-only diagnostic did not drain; this does not admit the whole Moogle effect.");
            Assert.True(privatePackets > 0);
            Assert.True(nativeMeshPackets > 0);
            Assert.Equal((ushort)0, frame.State.Work.PassEmitterCount);
            Assert.Equal((ushort)0, frame.State.Work.PassGeometryCount);
            if (sourceIndex == 0)
            {
                firstBank = JsonSerializer.Serialize(frame.State.Banks[0]);
            }
            else
            {
                Assert.Equal(firstBank, JsonSerializer.Serialize(frame.State.Banks[0]));
            }
        }
        NativeBattleMoogle96NodeState missing = frame.State with { Banks = frame.State.Banks.SetItem(0, frame.State.Banks[0] with { Data = null }) };
        Assert.Throws<NotSupportedException>(missing.Validate);
        Assert.Throws<InvalidDataException>(() => (frame.State with { Work = frame.State.Work with { InitializationFlag = 1, Data = frame.State.Banks[0].Data } }).Validate());
    }
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
}
