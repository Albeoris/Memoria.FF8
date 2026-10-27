using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco97NodeResourcesTests
{
    [Fact]
    public void Original_static_and_file448_banks_retain_distinct_complete_cold_sources()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ImmutableArray<NativeBattleChoco97NodeResource> resources = game.ReadNativeChoco97NodeResources();
        Assert.Equal(resources, game.ReadNativeChoco97NodeResources());
        Assert.Equal(new uint[] { 0x168C654, 0x19DFEF8 }, resources.Select(resource => resource.Address));
        Assert.Equal(new[] { 0x3268, 0x4A60 }, resources.Select(resource => resource.Data.Length));
        Assert.Equal(Assert.Single(game.ReadNativeChoco97IoResources().Files, file => file.FileId == 448).Data, resources[1].Data);
        Assert.All(resources, resource => Assert.Equal(0u, U32(resource.Data, 0)));
        Assert.Equal(new uint[] { 0x2BB0, 0x2D08, 0x2E60, 0x2FB8, 0, 0x3110 }, Enumerable.Range(0, 6).Select(index => U32(resources[0].Data, 4 + index * 4)));
        Assert.Equal(new byte[] { 4, 0, 0, 4, 4, 4, 0, 0 }, Enumerable.Range(0, 8).Select(index => resources[1].Data[checked((int)U32(resources[1].Data, 4 + index * 4) + 0x16)]));
        string json = SnapshotJson(resources);
        Assert.Equal(json, SnapshotJson(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<ImmutableArray<NativeBattleChoco97NodeResource>>(json))));
    }

    [Fact]
    public void Original_file448_model0_updates_all_96_FT4_U_faces_without_changing_any_other_byte()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ImmutableArray<byte> source = game.ReadNativeChoco97NodeResources()[1].Data;
        int resource = checked((int)U32(source, 0x84));
        Assert.Equal(0x74C, resource);
        Assert.Equal(153u, U32(source, resource + 4));
        int cursor = resource + checked((int)U32(source, resource)) / 4 * 4;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] expected = source.ToArray();
        int faces = 0;
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)U32(source, cursor));
            cursor += 4;
            if (group == 3)
            {
                Assert.Equal(96, count);
                for (int face = 0; face < count; face++)
                {
                    int start = cursor + face * strides[group];
                    int[] corners = [12, 16, 20, 22];
                    uint[] candidates = corners.Select(offset => unchecked((uint)((source[start + offset] & 127) - 8))).ToArray();
                    bool wrap = candidates.Any(value => value > 127);
                    for (int index = 0; index < corners.Length; index++)
                    {
                        expected[start + corners[index]] = (byte)((wrap ? unchecked(candidates[index] - 64) : candidates[index]) & 127);
                    }
                    faces++;
                }
            }
            else if (group is 2 or 6 or 7)
            {
                Assert.Equal(0, count);
            }
            cursor += count * strides[group];
        }
        Assert.Equal(96, faces);
        Assert.Equal(expected, NativeBattleChoco97Uv.Scroll72DF60(source, resource, -8));
    }

    [Fact]
    public void Static_initialization_is_independent_while_loaded_fixups_alias_IO_and_survive_JSON_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ImmutableArray<NativeBattleChoco97NodeResource> resources = game.ReadNativeChoco97NodeResources();
        NativeBattleChoco97IoResources ioResources = game.ReadNativeChoco97IoResources();
        NativeBattleChoco97IoBanks banks = new(0x19DB018, 0x20000, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x20000));
        uint[] addresses = [0x19DB018, 0x19DFEF8, 0x19E4958, 0x19E8660];
        int[] ids = [445, 448, 449, 450];
        for (int index = 0; index < ids.Length; index++)
        {
            banks = banks.Write(addresses[index], Assert.Single(ioResources.Files, file => file.FileId == ids[index]).Data);
        }
        NativeBattleChoco97IoState io = NativeBattleChoco97Io.Initialize729EF0(ioResources.Script.Data, 0x20DFAB8 + 0x20000, banks).State;
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 1, 97, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x64, 1), NativeBattleEffectTaskPool.Initialize(0x2579D38, 0x2579548, 0x2A4, 3)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 1, 0x64);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteWord(0x5A, 1));
        List<uint> contexts = [];
        foreach (NativeBattleChoco97NodeResource resource in resources)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(1, NativeBattleChoco97Nodes.Callback, 0x2A4, root.Address, (_, _, _) => new(0, 3));
            hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteDword(0x30, resource.Address).WriteWord(0x298, resource.Address == NativeBattleChoco97Nodes.StaticAddress ? (ushort)18 : (ushort)0).WriteWord(0x29C, 2).WriteWord(0x29E, 45));
            contexts.Add(appended.Address);
        }
        NativeBattleOriginalEffectState effect = new(97, 96, new(97, 0x729A60, 0x729A40), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x1FFF, [0xA5]).Write(0x13760, [0xB6]);
        NativeBattleChoco97NodeFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() }, effect, NativeBattleChoco97NodeState.FromColdProcess(), io, new(1));
        frame = NativeBattleChoco97Nodes.ClearStorage(frame, 0);
        Assert.Equal(0x2000, frame.State.Work.Emitters);
        Assert.Equal(0x30E0, frame.State.Work.Geometry);
        Assert.Equal(new byte[0x11760], frame.Memory.Arena!.Read(0x2000, 0x11760));
        Assert.Equal(0xA5, frame.Memory.Arena.Read(0x1FFF, 1)[0]);
        Assert.Equal(0xB6, frame.Memory.Arena.Read(0x13760, 1)[0]);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGraphicsModel Model(int index) => new(index, 3) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), GeometryWorldMatrix = identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)) };
        NativeBattleEffectNodeDogOrigins origins = new(new(-200, -400, 100), new(200, -768, 1000));
        NativeBattleChoco97NodeFrame Initialize(NativeBattleChoco97NodeFrame current, int index) => NativeBattleChoco97Nodes.InitializeContext(current, contexts[index], resources[index], packets, Model, _ => throw new InvalidOperationException("Mode2 consumes explicit origins."), origins);
        frame = Initialize(frame, 0);
        ImmutableArray<byte> staticData = NativeBattleStorageSnapshots.Capture(frame.State.StaticData!).Data;
        Assert.Equal(1u, U32(staticData, 0));
        Assert.Equal(resources[1].Data, frame.Io!.Banks.Read(0x19DFEF8, 0x4A60));
        NativeBattleChoco97NodeFrame cold = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleChoco97NodeFrame>(SnapshotJson(frame))!);
        NativeBattleChoco97NodeFrame loaded = Initialize(frame, 1);
        Assert.Equal(SnapshotJson(loaded), SnapshotJson(Initialize(cold, 1)));
        Assert.Equal(staticData, NativeBattleStorageSnapshots.Capture(loaded.State.StaticData!).Data);
        byte[] relocated = loaded.Io!.Banks.Read(0x19DFEF8, 0x4A60);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(relocated));
        Assert.Equal(0x19DFEF8u + U32(resources[1].Data, 4), BinaryPrimitives.ReadUInt32LittleEndian(relocated.AsSpan(4)));
        Assert.Null(loaded.State.Work.Data);
        Assert.Equal(0u, loaded.State.Work.InitializationFlag);
        foreach (int index in new[] { 0, 2, 3 })
        {
            ImmutableArray<byte> original = Assert.Single(ioResources.Files, file => file.FileId == ids[index]).Data;
            Assert.Equal(original, loaded.Io!.Banks.Read(addresses[index], original.Length));
        }
        Assert.Equal(SnapshotJson(loaded), SnapshotJson(Initialize(loaded, 1)));
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
                frame = NativeBattleChoco97Nodes.BeginPass(frame, view, 0x1000);
                string? continuation = callback % 17 == 0 ? SnapshotJson(frame) : null;
                NativeBattleChoco97NodeStep step = NativeBattleChoco97Nodes.AdvanceContext(frame, contexts[sourceIndex], Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
                if (callback % 17 == 0)
                {
                    NativeBattleChoco97NodeFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleChoco97NodeFrame>(continuation!)!);
                    NativeBattleChoco97NodeStep next = NativeBattleChoco97Nodes.AdvanceContext(restored, contexts[sourceIndex], Model, projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
                    Assert.Equal(SnapshotJson(step), SnapshotJson(next));
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
            Assert.Equal(staticData, NativeBattleStorageSnapshots.Capture(frame.State.StaticData!).Data);
        }
        Assert.False(relocated.AsSpan().SequenceEqual(frame.Io!.Banks.Read(0x19DFEF8, 0x4A60)));
        foreach (int index in new[] { 0, 2, 3 })
        {
            ImmutableArray<byte> original = Assert.Single(ioResources.Files, file => file.FileId == ids[index]).Data;
            Assert.Equal(original, frame.Io!.Banks.Read(addresses[index], original.Length));
        }
    }

    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
