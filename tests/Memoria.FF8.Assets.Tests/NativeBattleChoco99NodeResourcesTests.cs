using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99NodeResourcesTests
{
    [Fact]
    public void Original_sources_are_separate_authenticated_cold_banks()
    {
        using GameAssets game = Installed();
        ImmutableArray<NativeBattleChoco97NodeResource> resources = game.ReadNativeChoco99NodeResources();
        Assert.Equal(new uint[] { 0x166A9F0, 0x19D3018 }, resources.Select(resource => resource.Address));
        Assert.Equal(new[] { 0x3268, 0xA2B8 }, resources.Select(resource => resource.Data.Length));
        Assert.All(resources, resource => Assert.Equal(0u, U32(resource.Data, 0)));
        Assert.Equal(game.ReadNativeChoco98NodeResource().Data.ToArray(), resources[0].Data.ToArray());
        Assert.Equal(Assert.Single(game.ReadNativeChoco99IoResources().Files, file => file.FileId == 479).Data.ToArray(), resources[1].Data.ToArray());
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<NativeBattleChoco97NodeResource>>(JsonSerializer.Serialize(resources))));
    }

    [Fact]
    public void Static_initialization_is_independent_while_loaded_fixups_alias_IO_and_survive_JSON_next()
    {
        using GameAssets game = Installed();
        ImmutableArray<NativeBattleChoco97NodeResource> resources = game.ReadNativeChoco99NodeResources();
        NativeBattleChoco99IoResources ioResources = game.ReadNativeChoco99IoResources();
        NativeBattleChoco97IoBanks banks = new(0x19D3018, 0x1F560, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x1F560));
        uint[] addresses = [0x19D3018];
        int[] ids = [479];
        for (int index = 0; index < ids.Length; index++)
        {
            banks = banks.Write(addresses[index], Assert.Single(ioResources.Files, file => file.FileId == ids[index]).Data);
        }
        banks = banks.Write(0x19D3018 + 0x1F000, [0xBC]);
        NativeBattleChoco99IoState io = NativeBattleChoco99Io.Initialize718200(ioResources.Script.Data, 0x20DFAB8 + 0x20000, banks).State;
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 1, 99, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x64, 1), NativeBattleEffectTaskPool.Initialize(0x256C1E0, 0x256B9F0, 0x2A4, 3)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 1, 0x64);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteWord(0x5A, 1));
        List<uint> contexts = [];
        foreach (NativeBattleChoco97NodeResource resource in resources)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(1, NativeBattleChoco99Nodes.Callback, 0x2A4, root.Address, (_, _, _) => new(0, 3));
            hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteDword(0x30, resource.Address).WriteWord(0x298, resource.Address == NativeBattleChoco99Nodes.StaticAddress ? (ushort)18 : (ushort)0).WriteWord(0x29C, 2).WriteWord(0x29E, 45));
            contexts.Add(appended.Address);
        }
        NativeBattleOriginalEffectState effect = new(99, 98, new(99, 0x717D30, 0x717D10), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x1FFF, [0xA5]).Write(0x13760, [0xB6]);
        NativeBattleChoco99NodeFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() }, effect, NativeBattleChoco97NodeState.FromColdProcess(), io, new(1));
        frame = NativeBattleChoco99Nodes.ClearStorage(frame, 0);
        Assert.Equal(0x2000, frame.State.Work.Emitters);
        Assert.Equal(0x30E0, frame.State.Work.Geometry);
        Assert.Equal(new byte[0x11760], frame.Memory.Arena!.Read(0x2000, 0x11760));
        Assert.Equal(0xA5, frame.Memory.Arena.Read(0x1FFF, 1)[0]);
        Assert.Equal(0xB6, frame.Memory.Arena.Read(0x13760, 1)[0]);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGraphicsModel Model(int index) => new(index, 3) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), GeometryWorldMatrix = identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)) };
        NativeBattleEffectNodeDogOrigins origins = new(new(-200, -400, 100), new(200, -768, 1000));
        NativeBattleChoco99NodeFrame Initialize(NativeBattleChoco99NodeFrame current, int index) => NativeBattleChoco99Nodes.InitializeContext(current, contexts[index], resources[index], packets, Model, _ => throw new InvalidOperationException("Mode2 consumes explicit origins."), origins);
        frame = Initialize(frame, 0);
        ImmutableArray<byte> staticData = frame.State.StaticData!.Value;
        Assert.Equal(1u, U32(staticData, 0));
        Assert.Equal(resources[1].Data, frame.Io!.Banks.Read(0x19D3018, 0xA2B8));
        NativeBattleChoco99NodeFrame cold = JsonSerializer.Deserialize<NativeBattleChoco99NodeFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleChoco99NodeFrame loaded = Initialize(frame, 1);
        Assert.Equal(JsonSerializer.Serialize(loaded), JsonSerializer.Serialize(Initialize(cold, 1)));
        Assert.Equal(staticData, loaded.State.StaticData!.Value);
        byte[] relocated = loaded.Io!.Banks.Read(0x19D3018, 0xA2B8);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(relocated));
        for (int index = 0; index < 64; index++)
        {
            uint relative = U32(resources[1].Data, 4 + index * 4);
            Assert.Equal(relative == 0 ? 0u : 0x19D3018u + relative, BinaryPrimitives.ReadUInt32LittleEndian(relocated.AsSpan(4 + index * 4)));
        }
        Assert.Null(loaded.State.Work.Data);
        Assert.Equal(0u, loaded.State.Work.InitializationFlag);
        Assert.Equal(JsonSerializer.Serialize(loaded), JsonSerializer.Serialize(Initialize(loaded, 1)));
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleActionMatrix view = identity with { Z = 16384 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        frame = loaded;
        for (int sourceIndex = 0; sourceIndex < 2; sourceIndex++)
        {
            bool complete = false;
            int privatePackets = 0;
            int commonPackets = 0;
            int coldBoundaries = 0;
            for (int callback = 0; callback < 600 && !complete; callback++)
            {
                frame = frame with { Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess!.Initialize500400() } };
                frame = NativeBattleChoco99Nodes.BeginPass(frame, view, 0x1000);
                NativeBattleChoco99NodeStep step = NativeBattleChoco99Nodes.AdvanceContext(frame, contexts[sourceIndex], Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
                if (callback % 17 == 0)
                {
                    NativeBattleChoco99NodeFrame restored = JsonSerializer.Deserialize<NativeBattleChoco99NodeFrame>(JsonSerializer.Serialize(frame))!;
                    NativeBattleChoco99NodeStep next = NativeBattleChoco99Nodes.AdvanceContext(restored, contexts[sourceIndex], Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
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
        byte[] currentLoaded = frame.Io!.Banks.Read(0x19D3018, 0xA2B8);
        int changedCoordinates = 0;
        for (int offset = 0; offset < relocated.Length; offset++)
        {
            bool mutableCoordinate = CoordinateByte(offset, 0x2DE8) || CoordinateByte(offset, 0x38B0);
            if (mutableCoordinate)
            {
                if (relocated[offset] != currentLoaded[offset])
                {
                    changedCoordinates++;
                }
            }
            else
            {
                Assert.Equal(relocated[offset], currentLoaded[offset]);
            }
        }
        Assert.True(changedCoordinates > 0, "The original loaded node program must publish its reached XYZ morph into the current IO owner.");
        Assert.Equal(new byte[] { 0xBC }, frame.Io.Banks.Read(0x19D3018 + 0x1F000, 1));

        static bool CoordinateByte(int offset, int vertices) => offset >= vertices && offset < vertices + 100 * 8 && (offset - vertices) % 8 < 6;

    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }

    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
