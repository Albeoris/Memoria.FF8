using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSandStorm176CaptureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Both_funnel_copies_capture_all_authored_faces_in_both_packet_parities_with_native_RNG_and_writes(int parity)
    {
        NativeBattleSandStorm176Frame native = RunFunnels(false, parity, out var nativeBatches, out var nativeOrdering);
        string nativeFrame = NativeBattleTestSnapshots.Json(native);
        string nativePackets = string.Join("\n", nativeBatches.Select(batch => NativeBattleTestSnapshots.Json(batch)));
        string nativeLinks = NativeBattleTestSnapshots.Json(nativeOrdering);
        NativeBattleSandStorm176Frame captured = RunFunnels(true, parity, out var batches, out var ordering);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(nativePackets, string.Join("\n", batches.Select(batch => NativeBattleTestSnapshots.Json(batch))));
        Assert.Equal(nativeLinks, NativeBattleTestSnapshots.Json(ordering));
        Assert.NotEqual(new NativeCrtRandomState(123), captured.Random);
        Assert.Equal(2, batches.Count);
        HashSet<NativeBattlePilotVisualIdentity> identities = [];
        for (int copy = 0; copy < 2; copy++)
        {
            Assert.Equal(128, batches[copy].Attempts.Length);
            Assert.Equal(0, batches[copy].AllocationBytes);
            foreach (NativeBattleGfGeometryAttempt attempt in batches[copy].Attempts)
            {
                NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
                NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
                Assert.True(identities.Add(geometry.Identity));
                Assert.Equal(copy, geometry.Identity.Slot);
                Assert.Equal(copy == 0 ? 0x23742A0L : 0x23743F8L, geometry.Identity.ResourceGeneration);
                Assert.InRange(geometry.Identity.Primitive, 0, 127);
                Assert.Equal(new Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
                Assert.True(geometry.UseSourceDepths);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 projected = NativeBattlePilotVisualGeometry.Project(geometry.Corner(corner), Projection);
                    uint screen = primitive.Words[2 + corner * 2];
                    Assert.InRange(Math.Abs(projected.X - unchecked((short)screen)), 0, 0.51f);
                    Assert.InRange(Math.Abs(projected.Y - unchecked((short)(screen >> 16))), 0, 0.51f);
                }
            }
        }
        Assert.Equal(256, identities.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Real_native_sand_birth_reaches_row149_and_keeps_native_state_and_random(bool landing)
    {
        NativeBattleSandStorm176Frame Run(bool capture, out NativeBattlePilotVisualIdentity before, out NativeBattlePilotVisualIdentity after)
        {
            NativeBattleSandStorm176Frame frame = Fixture(0, landing ? 0x5D1820u : 0x5D11F0u, out var resources);
            NativeBattleEffectArena module = frame.Memory.SandStorm176!.Module;
            for (int row = 0; row < 149; row++)
            {
                module = module.Write(checked((int)(0x23685B8 - NativeBattleSandStorm176Controller.ModuleBase)) + row * 24, BitConverter.GetBytes(2u));
            }
            frame = frame with { Memory = frame.Memory with { SandStorm176 = frame.Memory.SandStorm176 with { Module = module } } };
            NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[0];
            before = NativeBattleSandStorm176VisualLifetimes.Identity(task, 149, landing, 0x123400);
            NativeBattleSandStorm176Frame result = NativeBattleSandStorm176Controller.Advance(frame, resources, Inputs(false, capture, (current, _) => current));
            after = NativeBattleSandStorm176VisualLifetimes.Identity(result.Effect.Pools[1].Slots[0], 149, landing, 0x123400);
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.SandStorm176!.Module.Read(checked((int)(0x23685B8 - NativeBattleSandStorm176Controller.ModuleBase)) + 149 * 24, 4)));
            return result;
        }
        NativeBattleSandStorm176Frame native = Run(false, out _, out _);
        string baseline = NativeBattleTestSnapshots.Json(native);
        NativeBattleSandStorm176Frame captured = Run(true, out var before, out var after);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.NotEqual(before.ResourceGeneration, after.ResourceGeneration);
        Assert.Equal(before.Generation, after.Generation);
        Assert.Equal((landing ? 150 : 0) + 149, after.Primitive);
        NativeBattleModulePoolTask task = captured.Effect.Pools[1].Slots[0];
        NativeBattleSandStorm176VisualLifetimes.Allocated(task, 149);
        Assert.NotEqual(after, NativeBattleSandStorm176VisualLifetimes.Identity(task, 149, landing, 0x123400));
    }

    private static NativeBattleActionMatrix View => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleCameraProjection Projection => new(View, 256, 160, 108);

    private static NativeBattleSandStorm176Inputs Inputs(bool drawOnly, bool capture, Func<NativeBattleSandStorm176Frame, NativeBattleSandStorm176Operation, NativeBattleSandStorm176Frame> execute) => new(NativeBattleActionPacketsState.Fresh(), Projection, 0, drawOnly, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), execute) { CapturePresentation = capture };

    private static NativeBattleSandStorm176Frame RunFunnels(bool capture, int parity, out List<NativeBattleGfGeometryBatch> batches, out NativeBattleLogicalOrdering ordering)
    {
        NativeBattleSandStorm176Frame frame = Fixture(parity, 0x5D0740, out var resources);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        (children, int second) = children.Append(0x5D0740);
        children = children.WriteSlot(second, children.Slots[second].WriteWord(12, 20).WriteWord(14, 1).WriteWord(16, 0).WriteWord(18, 0).WriteWord(20, 10000));
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        List<NativeBattleGfGeometryBatch> delivered = [];
        NativeBattleLogicalOrdering links = NativeBattleLogicalOrdering.Clear();
        NativeBattleSandStorm176Frame result = NativeBattleSandStorm176Controller.Advance(frame, resources, Inputs(true, capture, (current, operation) =>
        {
            NativeBattleSandStorm176Funnel draw = Assert.IsType<NativeBattleSandStorm176Funnel>(operation);
            delivered.Add(draw.Batch);
            NativeBattleSandStorm176State state = current.Memory.SandStorm176!;
            NativeBattleRegionalPacketWrite written = NativeBattleSandStorm176Packets.WriteFunnel(state.Module, links, draw.Batch);
            links = written.Ordering;
            return current with { Memory = current.Memory with { SandStorm176 = state with { Module = written.Arena } } };
        }));
        batches = delivered;
        ordering = links;
        return result;
    }

    private static NativeBattleSandStorm176Frame Fixture(int parity, uint callback, out NativeBattleSandStorm176Resources resources)
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x2368598, 0x2368588, 16, 1);
        (roots, int root) = roots.Append(0x5D1A60);
        roots = roots.WriteSlot(root, roots.Slots[root].WriteWord(12, (ushort)parity));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23733E8, 0x23733F8, 36, 100);
        (children, int first) = children.Append(callback);
        children = children.WriteSlot(first, children.Slots[first].WriteWord(12, callback == 0x5D0740 ? (ushort)20 : (ushort)0).WriteWord(14, 1).WriteWord(16, 0).WriteWord(18, 0).WriteWord(20, 10000));
        if (callback == 0x5D0740)
        {
            children = children.WriteSlot(first, children.Slots[first].WriteWord(14, 0));
        }
        NativeBattleEffectArena module = NativeBattleEffectArena.Clear();
        foreach (uint bank in new uint[] { 0x23693C8, 0x236E3C8 })
        {
            for (int packet = 0; packet < 512; packet++)
            {
                int offset = checked((int)(bank - NativeBattleSandStorm176Controller.ModuleBase)) + packet * 40;
                module = module.Write(offset, BitConverter.GetBytes(0x09000000u)).Write(offset + 4, BitConverter.GetBytes(0x2E808080u));
            }
        }
        NativeBattleOriginalEffectState effect = new(176, 175, new(176, 0x5D00F0, 0), new(0, 0, 0, 0, 0, 0), 0x2368598) { Pools = [roots, children], ResourcePointers = [new(0x2374554, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { SandStorm176 = new(module, View, false) };
        resources = new(null!, null!, View, 0, 0);
        return new(memory, effect, new(123));
    }
}
