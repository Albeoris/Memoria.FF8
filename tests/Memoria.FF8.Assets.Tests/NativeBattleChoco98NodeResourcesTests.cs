using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco98NodeResourcesTests
{
    [Fact]
    public void Original_static_mode2_nodes_keep_fixups_without_U_updates_and_emit_only_common_packets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco97NodeResource resource = game.ReadNativeChoco98NodeResource();
        Assert.Same(resource, game.ReadNativeChoco98NodeResource());
        Assert.Equal(0x167D934u, resource.Address);
        Assert.Equal(game.ReadNativeChoco97NodeResources()[0].Data.ToArray(), resource.Data.ToArray());
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 1, 98, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x64, 1), NativeBattleEffectTaskPool.Initialize(0x2573AB8, 0x25732C8, 0x2A4, 3)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 1, 0x64);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteWord(0x5A, 1));
        NativeBattleEffectHierarchyAppend context = hierarchy.Append8DC540(1, NativeBattleChoco98Nodes.Callback, 0x2A4, root.Address, (_, _, _) => new(0, 3));
        hierarchy = context.Hierarchy.Write(context.Address, context.Hierarchy.Read(context.Address).WriteDword(0x30, resource.Address).WriteWord(0x298, 18).WriteWord(0x29C, 2).WriteWord(0x29E, 45));
        NativeBattleOriginalEffectState effect = new(98, 97, new(98, 0x721860, 0x721840), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x1FFF, [0xA5]).Write(0x13760, [0xB6]);
        NativeBattleChoco98NodeFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() }, effect, NativeBattleChoco97NodeState.FromColdProcess(), new(1));
        frame = NativeBattleChoco98Nodes.ClearStorage(frame, 0);
        Assert.Equal(0x2000, frame.State.Work.Emitters);
        Assert.Equal(0x30E0, frame.State.Work.Geometry);
        Assert.Equal(new byte[0x11760], frame.Memory.Arena!.Read(0x2000, 0x11760));
        Assert.Equal(0xA5, frame.Memory.Arena.Read(0x1FFF, 1)[0]);
        Assert.Equal(0xB6, frame.Memory.Arena.Read(0x13760, 1)[0]);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGraphicsModel Model(int index) => new(index, 3) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), GeometryWorldMatrix = identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)) };
        NativeBattleEffectNodeDogOrigins origins = new(new(-200, -400, 100), new(200, -768, 1000));
        frame = NativeBattleChoco98Nodes.InitializeContext(frame, context.Address, resource, packets, Model, _ => throw new InvalidOperationException("Mode2 requires its explicit origins."), origins);
        ImmutableArray<byte> fixedData = NativeBattleStorageSnapshots.Capture(frame.State.StaticData!).Data;
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(fixedData.AsSpan()));
        Assert.Equal(resource.Address + BinaryPrimitives.ReadUInt32LittleEndian(resource.Data.AsSpan().Slice(4)), BinaryPrimitives.ReadUInt32LittleEndian(fixedData.AsSpan().Slice(4)));
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleActionMatrix view = identity with { Z = 16384 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        bool complete = false;
        int commonPackets = 0;
        int coldChecks = 0;
        for (int callback = 0; callback < 600 && !complete; callback++)
        {
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess!.Initialize500400() } };
            frame = NativeBattleChoco98Nodes.BeginPass(frame, view, 0x1000);
            string? continuation = callback % 17 == 0 ? SnapshotJson(frame) : null;
            NativeBattleChoco98NodeStep step = NativeBattleChoco98Nodes.AdvanceContext(frame, context.Address, Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
            if (callback % 17 == 0)
            {
                NativeBattleChoco98NodeFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleChoco98NodeFrame>(continuation!)!);
                Assert.Equal(SnapshotJson(step), SnapshotJson(NativeBattleChoco98Nodes.AdvanceContext(restored, context.Address, Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!)));
                coldChecks++;
            }
            Assert.Empty(step.PrivateSprites);
            commonPackets += step.CommonMeshes.Sum(batch => batch.Attempts.Count(attempt => attempt.Primitive is not null));
            Assert.Equal(0x1000, step.Frame.State.Work.PrivateDrawCursor);
            Assert.Equal(fixedData.ToArray(), NativeBattleStorageSnapshots.Capture(step.Frame.State.StaticData!).Data.ToArray());
            Assert.Null(step.Frame.State.Work.Data);
            Assert.Equal(0u, step.Frame.State.Work.InitializationFlag);
            frame = step.Frame;
            complete = step.Complete;
        }
        Assert.True(complete, "The node-only diagnostic exceeded its callback budget.");
        Assert.True(commonPackets > 0 && coldChecks > 1);
        Assert.Equal((ushort)0, frame.State.Work.PassEmitterCount);
        Assert.Equal((ushort)0, frame.State.Work.PassGeometryCount);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco98Nodes.InitializeContext(frame, context.Address, resource with { Address = 0x19DFEF8 }, packets, Model, _ => throw new InvalidOperationException(), origins));
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
