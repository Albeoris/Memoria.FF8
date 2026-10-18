using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco100NodeResourcesTests
{
    [Fact]
    public void Original_sources_are_separate_authenticated_cold_banks()
    {
        using GameAssets game = Installed();
        ImmutableArray<NativeBattleChoco97NodeResource> resources = game.ReadNativeChoco100NodeResources();
        Assert.Equal(new uint[] { 0x165FEEC, 0x19E3018, 0x19D5018 }, resources.Select(resource => resource.Address));
        Assert.Equal(new[] { 0x3268, 0x1958, 0x7660 }, resources.Select(resource => resource.Data.Length));
        Assert.All(resources, resource => Assert.Equal(0u, U32(resource.Data, 0)));
        Assert.Equal(game.ReadNativeChoco98NodeResource().Data.ToArray(), resources[0].Data.ToArray());
        Assert.Equal(Assert.Single(game.ReadNativeChoco100IoResources().Files, file => file.FileId == 495).Data.ToArray(), resources[1].Data.ToArray());
        Assert.Equal(Assert.Single(game.ReadNativeChoco100IoResources().Files, file => file.FileId == 501).Data.ToArray(), resources[2].Data.ToArray());
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<NativeBattleChoco97NodeResource>>(JsonSerializer.Serialize(resources))));
    }

    [Fact]
    public void Three_actual_sources_drain_through_shared_work_and_authoritative_IO_with_cold_next()
    {
        using GameAssets game = Installed();
        ImmutableArray<NativeBattleChoco97NodeResource> resources = game.ReadNativeChoco100NodeResources();
        NativeBattleChoco100IoResources ioResources = game.ReadNativeChoco100IoResources();
        NativeBattleChoco97IoBanks banks = new(0x19D5018, 0x22300, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x22300));
        uint[] addresses = [0x19E3018, 0x19D5018];
        int[] ids = [495, 501];
        for (int index = 0; index < ids.Length; index++)
        {
            banks = banks.Write(addresses[index], Assert.Single(ioResources.Files, file => file.FileId == ids[index]).Data);
        }
        banks = banks.Write(0x19D5018 + 0x22000, [0xBC]);
        NativeBattleChoco100IoState io = NativeBattleChoco100Io.Initialize70EE30(ioResources.Script.Data, 0x20DFAB8 + 0x20000, banks).State;
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 1, 100, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x64, 1), NativeBattleEffectTaskPool.Initialize(0x2563EC8, 0x2563438, 0x2A4, 4)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 1, 0x64);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteWord(0x5A, 1));
        List<uint> contexts = [];
        foreach (NativeBattleChoco97NodeResource resource in resources)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(1, NativeBattleChoco100Nodes.Callback, 0x2A4, root.Address, (_, _, _) => new(0, 3));
            hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteDword(0x30, resource.Address).WriteWord(0x298, resource.Address == NativeBattleChoco100Nodes.StaticAddress ? (ushort)18 : resource.Address == NativeBattleChoco100Nodes.Loaded501Address ? (ushort)15 : (ushort)0).WriteWord(0x29C, 2).WriteWord(0x29E, 45));
            contexts.Add(appended.Address);
        }
        NativeBattleOriginalEffectState effect = new(100, 99, new(100, 0x70D390, 0x70D370), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x1FFF, [0xA5]).Write(0x13760, [0xB6]);
        NativeBattleChoco100NodeFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() }, effect, NativeBattleChoco97NodeState.FromColdProcess(), io, new(1));
        frame = NativeBattleChoco100Nodes.ClearStorage(frame, 0);
        Assert.Equal(0x2000, frame.State.Work.Emitters);
        Assert.Equal(0x30E0, frame.State.Work.Geometry);
        Assert.Equal(new byte[0x11760], frame.Memory.Arena!.Read(0x2000, 0x11760));
        Assert.Equal(0xA5, frame.Memory.Arena.Read(0x1FFF, 1)[0]);
        Assert.Equal(0xB6, frame.Memory.Arena.Read(0x13760, 1)[0]);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGraphicsModel Model(int index) => new(index, 3) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), GeometryWorldMatrix = identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)) };
        NativeBattleEffectNodeDogOrigins origins = new(new(-200, -400, 100), new(200, -768, 1000));
        NativeBattleChoco100NodeFrame Initialize(NativeBattleChoco100NodeFrame current, int index) => NativeBattleChoco100Nodes.InitializeContext(current, contexts[index], resources[index], packets, Model, _ => throw new InvalidOperationException("Mode2 consumes explicit origins."), origins);
        List<byte[]> relocatedBanks = [];
        for (int source = 0; source < resources.Length; source++)
        {
            NativeBattleChoco100NodeFrame cold = JsonSerializer.Deserialize<NativeBattleChoco100NodeFrame>(JsonSerializer.Serialize(frame))!;
            NativeBattleChoco100NodeFrame initialized = Initialize(frame, source);
            Assert.Equal(JsonSerializer.Serialize(initialized), JsonSerializer.Serialize(Initialize(cold, source)));
            frame = initialized;
            byte[] relocated = source == 0 ? frame.State.StaticData!.Value.ToArray() : frame.Io!.Banks.Read(resources[source].Address, resources[source].Data.Length);
            relocatedBanks.Add(relocated);
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(relocated));
            for (int entry = 0; entry < 64; entry++)
            {
                uint relative = U32(resources[source].Data, 4 + entry * 4);
                Assert.Equal(relative == 0 ? 0u : resources[source].Address + relative, BinaryPrimitives.ReadUInt32LittleEndian(relocated.AsSpan(4 + entry * 4)));
            }
            Assert.Null(frame.State.Work.Data);
            Assert.Equal(0u, frame.State.Work.InitializationFlag);
        }
        ImmutableArray<byte> staticData = frame.State.StaticData!.Value;
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleActionMatrix view = identity with { Z = 16384 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        for (int sourceIndex = 0; sourceIndex < 3; sourceIndex++)
        {
            bool complete = false;
            int privatePackets = 0;
            int commonPackets = 0;
            int coldBoundaries = 0;
            for (int callback = 0; callback < 600 && !complete; callback++)
            {
                frame = frame with { Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess!.Initialize500400() } };
                frame = NativeBattleChoco100Nodes.BeginPass(frame, view, 0x1000);
                NativeBattleChoco100NodeStep step;
                try
                {
                    step = NativeBattleChoco100Nodes.AdvanceContext(frame, contexts[sourceIndex], Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException($"Native node source{resources[sourceIndex].Address:X} callback{callback} failed.", error);
                }
                if (callback % 17 == 0)
                {
                    NativeBattleChoco100NodeFrame restored = JsonSerializer.Deserialize<NativeBattleChoco100NodeFrame>(JsonSerializer.Serialize(frame))!;
                    NativeBattleChoco100NodeStep next = NativeBattleChoco100Nodes.AdvanceContext(restored, contexts[sourceIndex], Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
                    Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(next));
                    coldBoundaries++;
                }
                privatePackets += step.PrivateSprites.Sum(batch => batch.Attempts.Count(attempt => attempt.Primitive is not null));
                commonPackets += step.CommonMeshes.Sum(batch => batch.Attempts.Count(attempt => attempt.Primitive is not null));
                Assert.Equal(0x1000, step.Frame.State.Work.PrivateDrawCursor);
                Assert.Null(step.Frame.State.Work.Data);
                Assert.Equal(0u, step.Frame.State.Work.InitializationFlag);
                frame = step.Frame;
                complete = step.Complete;
            }
            Assert.True(complete, "The bounded original node-only diagnostic did not drain; this does not admit the whole Choco effect.");
            Assert.Equal(0, privatePackets);
            Assert.True(commonPackets > 0);
            Assert.True(coldBoundaries > 1);
            Assert.Equal((ushort)0, frame.State.Work.PassEmitterCount);
            Assert.Equal((ushort)0, frame.State.Work.PassGeometryCount);
            Assert.Equal(staticData, frame.State.StaticData!.Value);
        }
        for (int source = 1; source < resources.Length; source++)
        {
            Assert.Equal(relocatedBanks[source].Take(0x104), frame.Io!.Banks.Read(resources[source].Address, 0x104));
        }
        Assert.Equal(new byte[] { 0xBC }, frame.Io!.Banks.Read(0x19D5018 + 0x22000, 1));

    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }

    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
