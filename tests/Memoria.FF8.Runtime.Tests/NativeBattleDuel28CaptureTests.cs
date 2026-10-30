using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel28CaptureTests
{
    [Theory]
    [InlineData((ushort)212, false, false, false)]
    [InlineData((ushort)212, true, false, false)]
    [InlineData((ushort)215, false, false, false)]
    [InlineData((ushort)215, true, false, false)]
    [InlineData((ushort)215, false, true, false)]
    [InlineData((ushort)215, true, true, false)]
    [InlineData((ushort)215, false, false, true)]
    [InlineData((ushort)216, false, false, false)]
    [InlineData((ushort)216, true, false, false)]
    [InlineData((ushort)216, false, false, true)]
    public void Selected_morph_collapse_and_window9_paths_keep_detached_native_state_W_and_actual_geometry(ushort effect, bool morph, bool anchored, bool window9)
    {
        var native = Hierarchy(effect, morph, anchored, window9, false);
        string frozen = native.Native;
        var captured = Hierarchy(effect, morph, anchored, window9, true);
        Assert.Equal(frozen, captured.Native);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batch), NativeBattleTestSnapshots.Json(captured.Batch));
        NativeBattleGeometryPrimitive[] polygons = captured.Batch.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.NotEmpty(polygons);
        foreach (NativeBattleGeometryPrimitive primitive in polygons)
        {
            NativeBattlePilotVisualGeometry geometry = primitive.PilotGeometry!;
            Assert.Equal(effect, geometry.Identity.Effect);
            Assert.Equal(37, geometry.Identity.Generation);
            Assert.Equal(unchecked(0x1234L * 397 ^ captured.Mesh), geometry.Identity.ResourceGeneration);
            Assert.Equal((byte)4, geometry.CornerCount);
            Assert.True(geometry.A.Z > 0);
            Assert.Equal(primitive.VertexPayload4[0], (int)geometry.A.Z);
        }
        Assert.All(Enumerable.Range(0, 8), index => Assert.Equal(new byte[] { 0xA5, 0xA5 }, captured.Arena.Read(captured.MorphOffset + index * 8 + 6, 2)));
        if (effect == 216 && window9)
        {
            Assert.Equal(2, polygons.Length);
            NativeBattlePilotVisualGeometry first = polygons[0].PilotGeometry!;
            NativeBattlePilotVisualGeometry overlay = polygons[1].PilotGeometry!;
            Assert.Equal(first.B, overlay.A);
            Assert.Equal(first.C, overlay.B);
            Assert.Equal(first.C, overlay.C);
            Assert.Equal(first.D, overlay.D);
            Assert.Equal(new Vector4(polygons[1].VertexPayload4[0], polygons[1].VertexPayload4[1], polygons[1].VertexPayload4[2], polygons[1].VertexPayload4[3]), overlay.SourceDepths);
            Assert.True(overlay.UseSourceDepths);
            Assert.NotEqual(first.Identity.Primitive, overlay.Identity.Primitive);
            Assert.Null(captured.Batch.Attempts[^1].Primitive!.PilotGeometry);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Debris215_retains_real_depth_outside_native_zero_payload_and_preserves_rejection(bool rejected)
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x30010203);
        NativeBattleModuleProjectedVertex[] points = [new(0x000A000A, 800, rejected ? 0x20000u : 0), new(0x000A0064, 801, 0), new(0x0064000A, 802, 0)];
        NativeBattleGfGeometryBatch plain = NativeBattleDuel215ParticlePackets.Emit69CE00(face, index => points[index], 17, 2);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        NativeBattleGfGeometryBatch captured = NativeBattleDuel215ParticlePackets.Emit69CE00(face, index => points[index], 17, 2, pilotIdentity: new(215, 1, 2, 3, 4), pilotWorld: [new(10, 10, 800), new(100, 10, 801), new(10, 100, 802)]);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(captured.Attempts);
        if (rejected)
        {
            Assert.Null(attempt.Primitive);
        }
        else
        {
            Assert.Equal(new[] { 0, 0, 0, 0 }, attempt.Primitive!.VertexPayload4);
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive.PilotGeometry);
            Assert.Equal(new Vector4(800, 801, 802, 0), geometry.SourceDepths);
            Assert.True(geometry.UseSourceDepths);
        }
    }

    [Theory]
    [InlineData((ushort)212, 336)]
    [InlineData((ushort)216, 3668)]
    public void Echo_capture_records_only_the_real_written_history_bank_without_native_payload_changes(ushort effect, int offset)
    {
        NativeBattleModulePoolTask Task() => NativeBattleDuelEcho.Initialize6A1230(new(1, 0, null, 1, new byte[6116].ToImmutableArray()), offset, [0]);
        (byte[] Data, bool[] Known) Matrix(int _) => (new byte[32], Enumerable.Range(0, 32).Select(index => index is not (18 or 19)).ToArray());
        NativeBattleModulePoolTask plain = Task();
        NativeBattleModulePoolTask captured = Task();
        NativeBattleModulePoolTask Capture(NativeBattleModulePoolTask task, bool enabled) => effect == 212
            ? NativeBattleDuelEcho.Capture68ADF0(task, offset, 1, Matrix, enabled ? NativeBattleDuel28Presentation.Allocated : null)
            : NativeBattleDuelEcho.Capture697850(task, offset, 1, Matrix, enabled ? NativeBattleDuel28Presentation.Allocated : null);
        plain = Capture(plain, false);
        string frozen = NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(plain));
        captured = Capture(captured, true);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(captured)));
        int firstBank = offset + 136;
        long first = NativeBattleDuel28Presentation.Row(captured, firstBank, 0x123400);
        captured = Capture(captured, true);
        Assert.Equal(first, NativeBattleDuel28Presentation.Row(captured, firstBank, 0x123400));
        captured = Capture(captured, true);
        Assert.NotEqual(first, NativeBattleDuel28Presentation.Row(captured, firstBank, 0x123400));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Fixed216_flash_stays_discrete_and_keeps_exact_original_packet_words(int phase)
    {
        NativeBattleGfGeometryBatch plain = NativeBattleDuel216Drawing.Flash699EE0(phase);
        NativeBattleGfGeometryBatch captured = NativeBattleDuel216Drawing.Flash699EE0(phase, new(216, 1, 2, 3, phase));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry geometry = Assert.Single(captured.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(new Vector3(320, 216, 0), geometry.D);
        Assert.True(geometry.ScreenSpace);
        Assert.False(geometry.Continuous);
    }


    [Theory]
    [InlineData((ushort)212)]
    [InlineData((ushort)216)]
    public void Actual_echo_wrappers_capture_saved_pose_world_corners_with_native_quarter_depth_and_detached_parity(ushort effect)
    {
        EchoFrame plain = DrawEcho(PrepareEcho(effect), effect, false);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        EchoFrame captured = DrawEcho(PrepareEcho(effect), effect, true);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(2, captured.Batches.Length);
        NativeBattlePilotVisualGeometry first = captured.Batches[0].Attempts[0].Primitive!.PilotGeometry!;
        NativeBattlePilotVisualGeometry second = captured.Batches[1].Attempts[0].Primitive!.PilotGeometry!;
        Assert.Equal(new Vector3(-20, 0, 600), first.A);
        Assert.Equal(new Vector3(-90, 0, 400), second.A);
        Assert.True(first.Continuous);
        Assert.False(first.ScreenSpace);
        Assert.True(first.UseSourceDepths);
        Assert.Equal(new Vector4(150, 150, 150, 0), first.SourceDepths);
        Assert.Equal(new Vector4(100, 100, 100, 0), second.SourceDepths);
        Assert.NotEqual(first.Identity, second.Identity);
        Assert.Equal((byte)0, captured.Echo.ReadByte(effect == 212 ? 336 : 3668));
        Assert.Equal((byte)2, captured.Echo.ReadByte((effect == 212 ? 336 : 3668) + 1));
    }

    [Fact]
    public void Deformed216_reads_actual_mutated_arena_XYZ_preserves_W_and_native_detached_state()
    {
        var plain = Deformed216(false);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        var captured = Deformed216(true);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry geometry = Assert.Single(captured.Batch.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!.PilotGeometry!;
        Assert.Equal(new Vector3(-50, -50, 4096), geometry.A);
        Assert.Equal(new Vector3(50, -50, 4096), geometry.B);
        Assert.Equal(new Vector3(-50, 50, 4096), geometry.C);
        Assert.All(Enumerable.Range(0, 16), index => Assert.Equal(new byte[] { 0xA5, 0xA5 }, captured.Frame.Memory.Arena!.Read(11486 + index * 8, 2)));
    }

    [Fact]
    public void Rejected_echo_face_does_not_read_unreached_world_corners_or_change_native_context()
    {
        byte[] face = new byte[16];
        NativeBattleModulePoolTask Task() => new NativeBattleModulePoolTask(1, 0, null, 1, new byte[84].ToImmutableArray()).WriteWord(20, 1).WriteDword(32, 0x606060);
        List<int> plainReads = [];
        List<int> capturedReads = [];
        NativeBattleDuelEchoProjectedVertex Read(int vertex, List<int> reads)
        {
            reads.Add(vertex);
            return new(0, 0, 16);
        }
        NativeBattleDuelEchoPacketsResult plain = NativeBattleDuelEchoPackets.Emit68AAE0(Task(), 12, face.ToImmutableArray(), vertex => Read(vertex, plainReads), 17, 2, 0x155, 0x100);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        NativeBattleDuelEchoPacketsResult captured = NativeBattleDuelEchoPackets.Emit68AAE0(Task(), 12, face.ToImmutableArray(), vertex => Read(vertex, capturedReads), 17, 2, 0x155, 0x100, pilotIdentity: new(212, 1, 2, 3, 4), readWorld: _ => throw new InvalidOperationException("Rejected corners have no metadata lookup."));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(new[] { 0 }, capturedReads);
        Assert.Equal(plainReads, capturedReads);
        Assert.Empty(captured.Batch.Attempts);
    }

    [Fact]
    public void Module_sprite_ownership_uses_physical_allocation_refs_without_inspecting_capture_metadata()
    {
        NativeBattleGeometryPrimitive primitive = new(17, [0x09000000, 0x2C808080], [0, 0, 0, 0]);
        NativeBattleModulePacketBatch batch = new([new(primitive, 0, 0), new(primitive, 80, 0)], 120);
        ImmutableArray<NativeBattlePrimitiveReference> owned = NativeBattleDuel28Presentation.Record([], "20DFAB8", 0, 160, batch);
        Assert.Equal(new[] { 160, 240 }, owned.Select(item => item.ByteOffset));
        Assert.All(owned, item => Assert.Equal("20DFAB8", item.Bank));
        Assert.All(batch.Primitives, item => Assert.Null(item.Primitive.PilotGeometry));
    }

    private sealed record EchoFrame(NativeBattleModulePoolTask Echo, NativeBattleFileStaging Staging, ImmutableArray<NativeBattleGfGeometryBatch> Batches);

    private static EchoFrame PrepareEcho(ushort effect)
    {
        int offset = effect == 212 ? 336 : 3668;
        NativeBattleModulePoolTask echo = NativeBattleDuelEcho.Initialize6A1230(new(1, 0, null, 1, Enumerable.Repeat((byte)0xA5, 6116).ToImmutableArray()), offset, [1]);
        foreach ((int x, int z) in new (int, int)[] { (10, 400), (80, 600) })
        {
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Z = z }, null);
            echo = effect == 212 ? NativeBattleDuelEcho.Capture68ADF0(echo, offset, 3, _ => (packed.Bytes.ToArray(), packed.Known.ToArray())) : NativeBattleDuelEcho.Capture697850(echo, offset, 3, _ => (packed.Bytes.ToArray(), packed.Known.ToArray()));
        }
        uint temporary = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(temporary, Enumerable.Repeat((byte)0xA5, 116).ToArray()).MarkUnknown(temporary + 18, 2).WriteResourcePointer(temporary + 32, new(new(0, 0x123), null, 20));
        return new(echo, staging, []);
    }

    private static EchoFrame DrawEcho(EchoFrame frame, ushort effect, bool capture)
    {
        byte[] geometry = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, 1);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 8);
        Word(8, 1);
        Word(10, 1);
        Word(12, 3);
        short[] points = [-100, 0, 0, 100, 0, 0, 0, 100, 0];
        for (int index = 0; index < points.Length; index++)
        {
            Word(14 + index * 2, unchecked((ushort)points[index]));
        }
        Word(32, 1);
        Word(44, 0);
        Word(46, 1);
        Word(48, 2);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleGraphicsModel model = new(0, 3) { GeometryWorldMatrix = identity, SequenceState = new() { MeshMask124 = 1 }, Color40 = 0x606060 };
        return effect == 212
            ? NativeBattleDuelEchoDrawing.Draw6A2760(frame, model, 0x20E1678, 336, current => current.Echo, (current, task, _) => current with { Echo = task }, 0x1000000, (_, offset, length) => geometry.AsSpan(offset, length).ToArray(), 3, identity, new(identity, 256, 160, 108), (current, address, bytes) => current with { Staging = current.Staging.Write(address, bytes.AsSpan()) }, (current, address, length) => current.Staging.ReadSnapshot(address, length), (current, batch) => current with { Batches = current.Batches.Add(batch) }, 0x155, 0x100, current => current.Staging, (current, staging) => current with { Staging = staging }, capture ? new(effect, 1, 2, 3, 4) : null, bank => 1000 + bank)
            : NativeBattleDuelEchoDrawing.Draw697520(frame, model, 0x20E1678, 3668, current => current.Echo, (current, task, _) => current with { Echo = task }, 0x1000000, (_, offset, length) => geometry.AsSpan(offset, length).ToArray(), 3, identity, new(identity, 256, 160, 108), (current, address, bytes) => current with { Staging = current.Staging.Write(address, bytes.AsSpan()) }, (current, address, length) => current.Staging.ReadSnapshot(address, length), (current, batch) => current with { Batches = current.Batches.Add(batch) }, 0x155, 0x100, current => current.Staging, (current, staging) => current with { Staging = staging }, capture ? new(effect, 1, 2, 3, 4) : null, bank => 1000 + bank);

        void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(offset), value);
    }

    private static NativeBattleDuel216DrawingStep Deformed216(bool capture)
    {
        uint start = NativeBattleDuel216Resources.Address;
        byte[] resource = new byte[NativeBattleDuel216Resources.EndAddress - start];
        Span<byte> mesh = resource.AsSpan(checked((int)(0x113C3CC - start)));
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 136);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.Slice(4), 16);
        for (int vertex = 0; vertex < 16; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.Slice(8 + vertex * 8), vertex == 8 ? (short)-100 : (short)100);
            BinaryPrimitives.WriteInt16LittleEndian(mesh.Slice(12 + vertex * 8), vertex == 4 ? (short)100 : (short)-100);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.Slice(14 + vertex * 8), 0x4321);
        }
        BinaryPrimitives.WriteInt32LittleEndian(mesh.Slice(144), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.Slice(148), 0x24808080);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.Slice(152), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.Slice(154), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.Slice(156), 2);
        NativeBattleDuel216Resources data = new(resource.ToImmutableArray(), [], new("unused", []), []);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Duel216 = new(true, null, data), Arena = NativeBattleEffectArena.Clear().Write(11480, Enumerable.Repeat((byte)0xA5, 1216).ToArray()) };
        NativeBattleOriginalEffectState effect = new(216, 215, new(216, 1, 1), null, null);
        NativeBattleDuel216Frame frame = new(memory, effect, NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), NativeCrtRandomState.FromColdProcess());
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        return NativeBattleDuel216Drawing.DrawMorph6960E0(frame, 2, 5, false, new(0, 0, 4096), identity, new(identity, 256, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, capture ? new(216, 1, 2, 3, 4) : null);
    }

    private static (string Native, NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Arena, uint Mesh, int MorphOffset) Hierarchy(ushort effect, bool morph, bool anchored, bool window9, bool capture)
    {
        uint address = effect switch { 212 => NativeBattleDuel212Resources.Address, 215 => NativeBattleDuel215Resources.Address, _ => NativeBattleDuel216Resources.Address };
        uint end = effect switch { 212 => NativeBattleDuel212Resources.EndAddress, 215 => NativeBattleDuel215Resources.EndAddress, _ => NativeBattleDuel216Resources.EndAddress };
        uint sourceAddress = address + 0x40;
        uint meshAddress = address + 0x100;
        byte[] resource = new byte[end - address];
        BinaryPrimitives.WriteUInt32LittleEndian(resource.AsSpan(0x48), meshAddress - sourceAddress);
        resource[0x20] = window9 ? (byte)'9' : (byte)'0';
        Mesh(effect == 216 && window9).CopyTo(resource, 0x100);
        byte[] nodeBytes = new byte[44];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(24 + axis * 2), 4096);
        }
        if (morph)
        {
            BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(38), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(42), 1);
        }
        if (effect == 216 && window9)
        {
            BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(36), 1024);
        }
        NativeBattleGfAnimationNode node = new(0, 0, nodeBytes.ToImmutableArray());
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix basis = view with { Z = 4096 };
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        int morphOffset = effect == 212 ? 42008 : 60000;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(morphOffset, Enumerable.Repeat((byte)0xA5, 64).ToArray()) };
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, 1, 1), null, null);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleGfAnimationSource source = new(sourceAddress, resource.AsSpan(0x40).ToArray().ToImmutableArray());
        NativeBattlePilotVisualIdentity? identity = capture ? new(effect, 1, 2, 37, 4) { ResourceGeneration = 0x1234 } : null;
        if (effect == 212)
        {
            NativeBattleDuel212Resources data = new(resource.ToImmutableArray(), [], [], [], [], new("unused", [])) { Files = [] };
            NativeBattleDuel212Frame frame = new(memory with { Duel212 = new(true, null, data) }, selected, staging, NativeCrtRandomState.FromColdProcess());
            NativeBattleDuel212HierarchyStep result = NativeBattleDuel212Hierarchy.Draw6A2B80(frame, source, node, new(0, 0, 0, basis, "0", null, null, 0, 0, 0, false), 1, projection, colors, identity);
            return (NativeBattleTestSnapshots.Json(result), result.Batch, result.Frame.Memory.Arena!, meshAddress, morphOffset);
        }
        if (effect == 215)
        {
            NativeBattleDuel215Resources data = new(resource.ToImmutableArray(), [], new("unused", []), []);
            NativeBattleDuel215Frame frame = new(memory with { Duel215 = new(true, null, data) }, selected, staging, NativeCrtRandomState.FromColdProcess());
            NativeBattleDuel215Program context = new(basis, new(0, 0, 0), window9 ? "9" : "0", 0, window9 ? (short)4096 : (short)0, 0, NativeBattleEffectNodeMemory.ArenaAddress + (uint)morphOffset);
            NativeBattleDuel215HierarchyStep result = anchored ? NativeBattleDuel215Hierarchy.Draw69C6B0(frame, source, node, context, projection, colors, identity) : NativeBattleDuel215Hierarchy.Draw69C220(frame, source, node, context, projection, colors, identity);
            return (NativeBattleTestSnapshots.Json(result), result.Batch, result.Frame.Memory.Arena!, meshAddress, morphOffset);
        }
        else
        {
            NativeBattleDuel216Resources data = new(resource.ToImmutableArray(), [], new("unused", []), []);
            NativeBattleDuel216Frame frame = new(memory with { Duel216 = new(true, null, data) }, selected, staging, NativeCrtRandomState.FromColdProcess());
            NativeBattleDuel216Program context = new(basis, null, address + 0x20, 0, 0, 0, NativeBattleEffectNodeMemory.ArenaAddress + (uint)morphOffset);
            NativeBattleDuel216HierarchyStep result = NativeBattleDuel216Hierarchy.Draw6978F0(frame, source, node, context, projection, colors, identity);
            return (NativeBattleTestSnapshots.Json(result), result.Batch, result.Frame.Memory.Arena!, meshAddress, morphOffset);
        }
    }

    private static byte[] Mesh(bool gouraud)
    {
        const int count = 8;
        const int polygons = 12 + count * 16;
        byte[] mesh = new byte[polygons + 72];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, polygons);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), count);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(8), 2);
        short[] xy = [-100, -100, 100, -100, -100, 100, 100, 100, -100, 100, 100, 100, -100, -100, 100, -100];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                int at = 12 + (shape * count + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(at), (short)(xy[vertex * 2] + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(at + 2), xy[vertex * 2 + 1]);
                BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(at + 6), 0x4321);
            }
        }
        int group = gouraud ? 7 : 3;
        int face = polygons + group * 4 + 4;
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(face), gouraud ? 0x3C808080u : 0x2C808080u);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        return mesh;
    }
}
