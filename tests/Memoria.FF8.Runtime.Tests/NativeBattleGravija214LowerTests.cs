using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGravija214Controller;
using static Memoria.FF8.Runtime.NativeBattleGravija214Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGravija214LowerTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(-10)]
    public void Group16_warp_capture_retains_both_actual_screen_passes_and_all_native_stores(int x)
    {
        var resources = NativeBattleGravija214StateTests.Resources();
        List<NativeBattleGravija214DeliverBatch> nativePackets = [];
        List<NativeBattleGravija214DeliverBatch> capturedPackets = [];
        var native = DrawWarp69E020(Frame(resources, x, x), CopiedMeshAddress, CopiedMeshAddress + 8, 37, 1024, 64, 8, resources, Inputs((frame, operation) =>
        {
            nativePackets.Add(Assert.IsType<NativeBattleGravija214DeliverBatch>(operation));
            return frame;
        }));
        NativeBattlePilotVisualIdentity owner = new(214, 901, 2, 211, 0) { ResourceGeneration = CopiedMeshAddress };
        var captured = DrawWarp69E020(Frame(resources, x, x), CopiedMeshAddress, CopiedMeshAddress + 8, 37, 1024, 64, 8, resources, Inputs((frame, operation) =>
        {
            capturedPackets.Add(Assert.IsType<NativeBattleGravija214DeliverBatch>(operation));
            return frame;
        }), owner);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(nativePackets), NativeBattleTestSnapshots.Json(capturedPackets));
        List<NativeBattlePilotVisualIdentity> identities = [];
        foreach (var packet in capturedPackets)
        {
            var primitive = Assert.Single(packet.Batch.Attempts).Primitive!;
            if (packet.Flags == 0)
            {
                Assert.Null(primitive.PilotGeometry);
                continue;
            }
            var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.True(visual.ScreenSpace);
            Assert.True(visual.UseSourceDepths);
            Assert.Equal(System.Numerics.Vector4.Zero, visual.SourceDepths);
            Assert.Empty(primitive.VertexPayload4);
            Assert.Equal(owner.Generation, visual.Identity.Generation);
            Assert.Equal(owner.ResourceGeneration, visual.Identity.ResourceGeneration);
            identities.Add(visual.Identity);
            for (int corner = 0; corner < visual.CornerCount; corner++)
            {
                uint screen = primitive.Words[2 + corner * 2];
                Assert.Equal(new System.Numerics.Vector3(unchecked((short)screen), unchecked((short)(screen >> 16)), 0), visual.Corner(corner));
            }
        }
        Assert.NotEmpty(identities);
        Assert.Equal(identities.Count, identities.Distinct().Count());
    }

    [Fact]
    public void Preprocessing_rotates_current_G3_G4_and_converts_indices_once()
    {
        var resources = NativeBattleGravija214StateTests.Resources() with { SqrtTable = Enumerable.Repeat((ushort)4096, 192).ToImmutableArray(), SineTable = Enumerable.Repeat((short)4096, 4096).ToImmutableArray() };
        var frame = Frame(resources);
        short[] xs = [-100, 100, -200, 200];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            frame = frame with { State = frame.State.WriteWord(CopiedMeshAddress + 8 + (uint)(vertex * 8), unchecked((ushort)xs[vertex])) };
        }
        uint triangle = CopiedMeshAddress + 1956;
        uint quad = CopiedMeshAddress + 2440;
        for (int corner = 0; corner < 4; corner++)
        {
            if (corner < 3)
            {
                frame = frame with { State = frame.State.WriteWord(triangle + 4 + (uint)(corner * 2), (ushort)(corner * 2)).WriteDword(triangle + (uint)(corner == 0 ? 0 : 8 + corner * 4), (uint)(100 + corner)) };
            }
            frame = frame with { State = frame.State.WriteWord(quad + 4 + (uint)(corner * 2), (ushort)(corner * 2)).WriteDword(quad + (uint)(corner == 0 ? 0 : 8 + corner * 4), (uint)(200 + corner)) };
        }
        var actual = PreprocessCopiedMesh69E940(frame, resources);
        Assert.Equal(new ushort[] { 6, 0, 3 }, Enumerable.Range(0, 3).Select(corner => actual.State.ReadWord(triangle + 4 + (uint)(corner * 2))));
        Assert.Equal(new ushort[] { 6, 0, 9, 3 }, Enumerable.Range(0, 4).Select(corner => actual.State.ReadWord(quad + 4 + (uint)(corner * 2))));
        Assert.Equal(202u, actual.State.ReadDword(quad));
        Assert.Equal(200u, actual.State.ReadDword(quad + 12));
        Assert.Equal(203u, actual.State.ReadDword(quad + 16));
        Assert.Equal(201u, actual.State.ReadDword(quad + 20));
        Assert.Equal(40000u, actual.Memory.GeometryRegisters!.Lzcs);
        Assert.Equal(16u, actual.Memory.GeometryRegisters.Lzcr);
        Assert.Equal(frame.Memory.ScriptedColorRegisters, actual.Memory.ScriptedColorRegisters);
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Zero_radial_rows_clear_packed_data_without_publishing_LZ_or_color_registers()
    {
        var resources = NativeBattleGravija214StateTests.Resources();
        var frame = Frame(resources);
        frame = frame with { State = frame.State.WriteDword(CopiedMeshAddress + 12, 0xFFFFFFFF), Memory = frame.Memory with { GeometryRegisters = frame.Memory.GeometryRegisters! with { Lzcs = 0x1234, Lzcr = 7 } } };
        var actual = PreprocessCopiedMesh69E940(frame, resources);
        Assert.Equal(0u, actual.State.ReadDword(CopiedMeshAddress + 12));
        Assert.Equal(0x1234u, actual.Memory.GeometryRegisters!.Lzcs);
        Assert.Equal(7u, actual.Memory.GeometryRegisters.Lzcr);
        Assert.Equal(frame.Memory.ScriptedColorRegisters, actual.Memory.ScriptedColorRegisters);
    }

    [Fact]
    public void Warp_emits_color_then_texture_pass_with_literal_modes_and_partial_masks()
    {
        var resources = NativeBattleGravija214StateTests.Resources();
        var frame = Frame(resources);
        List<NativeBattleGravija214DeliverBatch> operations = [];
        var actual = DrawWarp69E020(frame, CopiedMeshAddress, CopiedMeshAddress + 8, 37, 1024, 0, 0, resources, Inputs((current, operation) =>
        {
            operations.Add(Assert.IsType<NativeBattleGravija214DeliverBatch>(operation));
            return current;
        }));
        Assert.Equal(482, operations.Count);
        Assert.Equal(0xE1000220u, Word(operations[0].Batch.Attempts[0], 4));
        Assert.Equal(0xE1000240u, Word(operations[241].Batch.Attempts[0], 4));
        Assert.Equal((ushort)0, operations[0].Flags);
        Assert.Equal((ushort)1, operations[1].Flags);
        Assert.Equal(0x32, operations[1].Batch.Attempts[0].Data[7]);
        Assert.Equal(0x3A, operations[25].Batch.Attempts[0].Data[7]);
        Assert.Equal(0x26, operations[242].Batch.Attempts[0].Data[7]);
        Assert.Equal(0x2E, operations[266].Batch.Attempts[0].Data[7]);
        var textured = operations[242].Batch.Attempts[0];
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[14]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[15]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[31]);
        Assert.Empty(textured.Primitive!.VertexPayload4);
        Assert.Equal(new[] { 0, 0, 0, 0 }, operations[0].Batch.Attempts[0].Primitive!.VertexPayload4);
        Assert.Equal(FirstPacketOffset + ArenaAddress + 16u + 24u * 28 + 216u * 36 + 24u * 32 + 216u * 40, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(0u, actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(1024u, actual.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Fact]
    public void Warp_clip_rejects_only_textured_pass_and_preserves_outside_X_padding()
    {
        var resources = NativeBattleGravija214StateTests.Resources();
        var frame = Frame(resources, -10, -20);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(ProjectedRowsOffset + 8, new byte[] { 0x10, 0xAB, 0xCD, 0xEF }) } };
        List<NativeBattleGravija214DeliverBatch> operations = [];
        var actual = DrawWarp69E020(frame, CopiedMeshAddress, CopiedMeshAddress + 8, 17, 0, 0, 0, resources, Inputs((current, operation) =>
        {
            operations.Add(Assert.IsType<NativeBattleGravija214DeliverBatch>(operation));
            return current;
        }));
        Assert.Equal(242, operations.Count);
        Assert.Equal(new byte[] { 0x15, 0xAB, 0xCD, 0xEF }, actual.Memory.Arena!.Read(ProjectedRowsOffset + 8, 4));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.Arena.Read(ProjectedRowsOffset + 4, 2)));
        Assert.Equal((short)-20, BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.Arena.Read(ProjectedRowsOffset + 6, 2)));
    }

    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(1, 2, 2)]
    public void Sprite_draw_precedes_angle_and_age_update_without_pause_gate(ushort oldAge, ushort lifetime, int result)
    {
        var resources = NativeBattleGravija214StateTests.Resources();
        var frame = Frame(resources);
        var pool = frame.Effect.Pools[1];
        (pool, _) = pool.Append(0x69DF00);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        frame = Write(frame, ArenaAddress, Read(frame, ArenaAddress).WriteWord(12, oldAge).WriteWord(14, lifetime).WriteWord(16, 0).WriteWord(18, 4096).WriteWord(20, 0).WriteWord(22, 0).WriteWord(24, 0));
        frame = Write(frame, ArenaAddress + 0x700, Read(frame, ArenaAddress + 0x700).WriteWord(14, 4096).WriteWord(40, 0).WriteWord(42, 0).WriteWord(44, 3000));
        bool called = false;
        var actual = AdvanceSprite69DF00(frame, ArenaAddress, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleGravija214DrawSprite180>(operation);
            Assert.Equal(oldAge, Read(current, ArenaAddress).ReadWord(12));
            Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            called = true;
            return current;
        }));
        Assert.True(called);
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)(oldAge + 1), Read(actual.Frame, ArenaAddress).ReadWord(12));
        Assert.Equal((ushort)8, Read(actual.Frame, ArenaAddress).ReadWord(16));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4096, 1)]
    [InlineData(1024, -1)]
    public void Programme_shape_selection_uses_physical_endpoints_or_XYZ_only_scratch(int ratio, int selected)
    {
        var resources = NativeBattleGravija214StateTests.Resources();
        byte[] bytes = resources.Programme.Data.ToArray();
        int geometry = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 72; vertex++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(geometry + 12 + (shape * 72 + vertex) * 8), (short)(100 + shape * 200));
            }
        }
        resources = resources with { Programme = resources.Programme with { Data = bytes.ToImmutableArray() } };
        var frame = Frame(resources);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(ScratchOffset + 6, BitConverter.GetBytes((ushort)0xBEEF)) } };
        byte[] nodeBytes = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(24), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(28), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(38), (short)ratio);
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(42), 1);
        var node = new NativeBattleGfAnimationNode(0, 0, nodeBytes.ToImmutableArray());
        bool called = false;
        var actual = DrawProgrammeNode214(frame, resources.Programme, node, new(1, 2, 3000), -128, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleGravija214DrawMesh7043B0>(operation);
            Assert.Equal(selected < 0 ? ArenaAddress + ScratchOffset : ProgrammeAddress + (uint)geometry + 12 + (uint)(selected * 72 * 8), draw.VertexAddress);
            Assert.Equal(0x2030u, BinaryPrimitives.ReadUInt32LittleEndian(Context(current, draw.TemporaryAddress, 88).Read(28, 4)));
            Assert.Equal(-128, BinaryPrimitives.ReadInt32LittleEndian(Context(current, draw.TemporaryAddress, 88).Read(24, 4)));
            called = true;
            return current;
        }));
        Assert.True(called);
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.Arena!.Read(ScratchOffset + 6, 2)));
        if (selected < 0)
        {
            Assert.Equal((short)150, BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.Arena.Read(ScratchOffset, 2)));
            Assert.Equal(1024u, actual.Memory.ScriptedColorRegisters!.Ir0);
        }
    }

    [Fact]
    public void Bounds_reads_quarter_depth_before_signed_radius_and_point_admission()
    {
        var frame = Frame(NativeBattleGravija214StateTests.Resources(), 0, 0);
        var rect = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)32767)).Write(2, BitConverter.GetBytes((short)32767)).Write(4, BitConverter.GetBytes((short)-32767)).Write(6, BitConverter.GetBytes((short)-32767));
        var inputs = Inputs((current, _) => current) with { ReadModel = (_, pointer) => new(0, 0) { Transform = new(new(0, 0, 0), new(0, 0, 1000), 0, 0, 0) } };
        var actual = Bounds69E860(frame, 0x1D972C0, rect, 65536, inputs);
        Assert.True(actual.Included);
        Assert.Equal(1000, actual.MinimumDepth);
        Assert.Equal((short)147, BinaryPrimitives.ReadInt16LittleEndian(actual.Rect8.Read(0, 2)));
        Assert.Equal((short)173, BinaryPrimitives.ReadInt16LittleEndian(actual.Rect8.Read(4, 2)));
        Assert.Equal(4000u, actual.Frame.Memory.GeometryRegisters!.Sz[3]);
    }

    [Theory]
    [InlineData(0x200u, true)]
    [InlineData(0u, false)]
    public void Programme_direct_caller_branch_publishes_actual_mean_W_only(uint header, bool knownW)
    {
        var resources = NativeBattleGravija214StateTests.Resources();
        var frame = Frame(resources);
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        var node = new NativeBattleGfAnimationNode(0, 0, bytes.ToImmutableArray());
        var actual = DrawProgrammeNode214(frame, resources.Programme, node, new(1, 2, 3000), -128, resources, Inputs((current, _) => current), unchecked((short)0xBEEF));
        var colors = actual.Memory.ScriptedColorRegisters!;
        Assert.Equal(knownW, colors.NormalInputKnown!.Value[6]);
        Assert.Equal(knownW, colors.NormalInputKnown.Value[7]);
        if (knownW)
        {
            Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(colors.NormalInput!.Value.AsSpan().Slice(6)));
        }
    }

    private static uint Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));
    private static NativeBattleGravija214Frame Frame(NativeBattleGravija214Resources resources, int x = 100, int y = 100)
    {
        var pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        var effect = new NativeBattleOriginalEffectState(214, 213, new(214, Entry, Initializer), null, 0) { Pools = pools };
        var matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Y = y, Z = 3000 };
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return new(memory, effect, new(123), NativeBattleGravija214State.FromColdProcess().CopyEntrySnapshot(resources).WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleGravija214Inputs Inputs(Func<NativeBattleGravija214Frame, NativeBattleGravija214Operation, NativeBattleGravija214Frame> execute) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (current, _) => new(current, PointView(new(0, 0, 0))), _ => NativeBattleActionPacketsState.Fresh(), execute) { ReadOrderingTable = _ => NativeBattlePrimitiveOrdering.InitializeBattle(0).TableAddress };
}
