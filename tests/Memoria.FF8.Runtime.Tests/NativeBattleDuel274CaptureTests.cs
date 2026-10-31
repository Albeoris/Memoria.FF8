using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274CaptureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Actual_selected_mesh_corners_and_depths_preserve_native_reads_material_and_packet_masks(bool quad, bool window)
    {
        ImmutableArray<byte> mesh = Mesh(quad, window);
        List<int> reads = [];
        NativeBattleVector Read(int offset)
        {
            reads.Add(offset);
            return offset switch { 0 => new(0, 0, 0), 8 => new(100, 0, 10), 16 => new(0, 100, 20), _ => new(100, 100, 30) };
        }
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 3000 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 288, 160, 108);
        NativeBattleGfGeometryBatch Run(NativeBattlePilotVisualIdentity? identity) => window ? NativeBattleDuel274MeshPackets.EmitBatch68C320(mesh, matrix, projection, 120, Read, pilotIdentity: identity) : NativeBattleDuel274MeshPackets.EmitBatch68BD50(mesh, matrix, projection, 0, 0x20, Read, pilotIdentity: identity);
        NativeBattleGfGeometryBatch plain = Run(null);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        int[] actualReads = reads.ToArray();
        reads.Clear();
        NativeBattlePilotVisualIdentity identity = new(274, 0x2000, 1, 2, 3) { ResourceGeneration = 0x10E9530 };
        NativeBattleGfGeometryBatch captured = NativeBattleCinematicActorPresentation.NativeDepths(Run(identity));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(actualReads, reads);
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null)).Primitive!;
        NativeBattlePilotVisualGeometry geometry = primitive.PilotGeometry!;
        Assert.Equal((ushort)274, geometry.Identity.Effect);
        Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
        Assert.Equal(new Vector3(0, 0, 3000), geometry.A);
        Assert.Equal(new Vector3(100, 0, 3010), geometry.B);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(primitive.VertexPayload4[0], geometry.SourceDepths!.Value.X);
    }

    [Fact]
    public void Rejected_custom_quad_does_not_read_a_fourth_selected_vertex_for_capture()
    {
        ImmutableArray<byte> mesh = Mesh(true, false);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = -3000 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 288, 160, 108);
        List<int> reads = [];
        NativeBattleGfGeometryBatch captured = NativeBattleDuel274MeshPackets.EmitBatch68BD50(mesh, matrix, projection, 0, 0, offset =>
        {
            reads.Add(offset);
            if (offset == 24)
            {
                throw new InvalidOperationException("Unreached fourth corner was read.");
            }
            return new(0, 0, 0);
        }, pilotIdentity: new(274, 0x2000, 1, 2, 3));
        Assert.Equal(new[] { 0, 8, 16 }, reads);
        Assert.Equal(0, captured.AllocationBytes);
        Assert.All(captured.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    [Fact]
    public void Echo_actual_captured_bank_births_preserve_detached_arena_state_unknown_padding_and_cold_roundtrip()
    {
        NativeBattleDuel274Frame Run(bool capture)
        {
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x4000 + 154, 2);
            NativeBattleDuel274Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena }, new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), 0), NativeBattleFileStaging.Unknown(), new(1)) { CapturePresentation = capture };
            frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(frame, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000, [9]);
            return NativeBattleDuel274ArenaEcho.Capture68ADF0(frame, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000, 10, bone =>
            {
                Assert.Equal(9, bone);
                byte[] data = new byte[32];
                bool[] known = Enumerable.Repeat(true, 32).ToArray();
                known[18] = false;
                known[19] = false;
                return (data, known);
            });
        }
        NativeBattleDuel274Frame plain = Run(false);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        NativeBattleDuel274Frame captured = Run(true);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        NativeBattleDuel274Frame cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(frozen)!;
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(cold));
        Assert.Throws<NotSupportedException>(() => captured.Memory.Arena!.Read(0x4000 + 154, 2));
        uint echo = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;
        long first = NativeBattlePilotVisualLifetimes.Identity(274, captured.Memory.Arena!, echo, 0).Generation;
        NativeBattlePilotVisualLifetimes.Allocated(captured.Memory.Arena!, echo, 0);
        Assert.NotEqual(first, NativeBattlePilotVisualLifetimes.Identity(274, captured.Memory.Arena!, echo, 0).Generation);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
    }

    [Fact]
    public void Fixed_screen_packets_remain_discrete_and_keep_original_packet_bytes()
    {
        NativeBattleGfGeometryBatch plain = NativeBattleDuel274Screen.Draw68D4B0(7);
        NativeBattleGfGeometryBatch captured = NativeBattleDuel274Presentation.Screen(plain, new(274, 0x2000, 1, 2, 3));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        Assert.All(captured.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null), attempt =>
        {
            Assert.False(attempt.Primitive!.PilotGeometry!.Continuous);
            Assert.True(attempt.Primitive.PilotGeometry.ScreenSpace);
            Assert.Equal(Vector4.Zero, attempt.Primitive.PilotGeometry.SourceDepths);
        });
        Assert.Equal(2, captured.Attempts.Count(attempt => attempt.Primitive?.PilotGeometry is not null));
    }

    [Fact]
    public void Actual_particle_task_source_identity_and_nested_callback_keep_detached_native_state()
    {
        NativeBattlePilotVisualIdentity? observed = null;
        NativeBattleDuel274Frame Run(bool capture)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508498, 0x20E3AB8, 36, 1);
            (pool, int slot) = pool.Append(0x68B140);
            NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, [pool]);
            NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), pool.DescriptorAddress) { Pools = [pool] };
            NativeBattlePilotVisualIdentity parent = new(274, 0x2000, 1, 2, 3);
            NativeBattleDuel274Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown(), new(1)) { CapturePresentation = capture, PilotIdentity = capture ? parent : null };
            NativeBattleDuel274Frame result = NativeBattleDuel274Banks.Advance508420(frame, pool.DescriptorAddress, (current, selected) =>
            {
                NativeBattleEffectTaskPool currentPool = current.Effect.Pools[0];
                NativeBattleModulePoolTask task = currentPool.Slots[selected];
                if (capture)
                {
                    observed = current.PilotIdentity;
                    NativeBattlePilotVisualIdentity source = NativeBattleDuel274Presentation.Identity(task, 0x10E7868);
                    Assert.Equal(source.Generation, observed!.Value.Generation);
                    Assert.Equal(source.Bank, observed.Value.Bank);
                    Assert.NotEqual(source.ResourceGeneration, NativeBattleDuel274Presentation.Identity(task, 0x10E7968).ResourceGeneration);
                }
                return new(current, currentPool.WriteSlot(selected, task.WriteWord(12, 17)), 2);
            });
            Assert.Equal(capture ? (NativeBattlePilotVisualIdentity?)parent : null, result.PilotIdentity);
            return result;
        }
        NativeBattleDuel274Frame plain = Run(false);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        NativeBattleDuel274Frame captured = Run(true);
        Assert.NotNull(observed);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
    }

    [Fact]
    public void Actual_echo_draw_captures_saved_pose_worlds_and_native_quarter_depth_with_detached_cold_parity()
    {
        (NativeBattleDuel274Frame Frame, List<NativeBattleGfGeometryBatch> Batches) Run(bool capture, bool cold)
        {
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25084B8, 0x25084C8, 24, 1);
            (root, _) = root.Append(0x689990);
            NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), root.DescriptorAddress) { Pools = [root] };
            NativeBattleDuel274Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), new(1)) { CapturePresentation = capture };
            uint echo = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;
            frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(frame, echo, [0]);
            frame = NativeBattleDuel274ArenaEcho.Capture68ADF0(frame, echo, 1, _ =>
            {
                byte[] matrix = new byte[32];
                BinaryPrimitives.WriteInt16LittleEndian(matrix, 4096);
                BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(8), 4096);
                BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(16), 4096);
                BinaryPrimitives.WriteInt32LittleEndian(matrix.AsSpan(28), 400);
                bool[] known = Enumerable.Repeat(true, 32).ToArray();
                known[18] = false;
                known[19] = false;
                return (matrix, known);
            });
            if (cold)
            {
                frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(frame))! with { CapturePresentation = capture };
            }
            if (capture)
            {
                frame = frame with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(274, frame.Effect.Pools[0].Slots[0]) };
            }
            byte[] section = new byte[60];
            BinaryPrimitives.WriteInt32LittleEndian(section, 1);
            BinaryPrimitives.WriteInt32LittleEndian(section.AsSpan(4), 8);
            BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(8), 1);
            BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(10), 0);
            BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(12), 3);
            NativeBattleVector[] points = [new(0, 0, 400), new(100, 0, 400), new(0, 100, 400)];
            for (int index = 0; index < points.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(14 + index * 6), points[index].X);
                BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(16 + index * 6), points[index].Y);
                BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(18 + index * 6), points[index].Z);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(32), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(46), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(48), 2);
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleGraphicsModel model = new(0, 3) { GeometryWorldMatrix = identity, SequenceState = new() { MeshMask124 = 1 }, Color40 = 0x808080 };
            List<NativeBattleGfGeometryBatch> batches = [];
            frame = NativeBattleDuel274Echo.Draw68A7B0(frame, model, echo, 0x1000000, (_, offset, length) => section.AsSpan(offset, length).ToArray(), 3, identity, new(identity, 256, 160, 108), (current, batch) =>
            {
                batches.Add(batch);
                return current;
            }, 0x155, 0x100);
            return (frame, batches);
        }
        var native = Run(false, false);
        string frozenFrame = NativeBattleTestSnapshots.Json(native.Frame);
        string frozenPackets = NativeBattleTestSnapshots.Json(native.Batches);
        var captured = Run(true, false);
        Assert.Equal(frozenFrame, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(frozenPackets, NativeBattleTestSnapshots.Json(captured.Batches));
        var cold = Run(true, true);
        Assert.Equal(frozenFrame, NativeBattleTestSnapshots.Json(cold.Frame));
        Assert.Equal(frozenPackets, NativeBattleTestSnapshots.Json(cold.Batches));
        foreach (var result in new[] { captured, cold })
        {
            NativeBattleGeometryPrimitive primitive = Assert.Single(Assert.Single(result.Batches).Attempts).Primitive!;
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal((ushort)274, geometry.Identity.Effect);
            Assert.False(geometry.ScreenSpace);
            Assert.True(geometry.Continuous);
            Assert.Equal(new Vector3(0, 0, 800), geometry.A);
            Assert.Equal(new Vector3(100, 0, 800), geometry.B);
            Assert.Equal(new Vector3(0, 100, 800), geometry.C);
            Assert.Equal(new Vector4(200, 200, 200, 0), geometry.SourceDepths);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(new[] { 200, 200, 200, 0 }, primitive.VertexPayload4);
        }
    }

    private static ImmutableArray<byte> Mesh(bool quad, bool window)
    {
        byte[] bytes = new byte[160];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        int cursor = 32;
        int groupSelected = window ? 2 : quad ? 5 : 4;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), group == groupSelected ? 1 : 0);
            cursor += 4;
            if (group != groupSelected)
            {
                continue;
            }
            int stride = quad ? 24 : 20;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), window ? 0x24808080u : quad ? 0x3A303030u : 0x32303030u);
            for (int corner = 0; corner < (quad ? 4 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + corner * 2), checked((ushort)(corner * 2)));
            }
            cursor += stride;
        }
        return bytes.ToImmutableArray();
    }
}
