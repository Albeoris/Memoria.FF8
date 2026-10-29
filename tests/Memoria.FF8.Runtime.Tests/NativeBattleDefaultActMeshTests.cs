using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDefaultActMeshTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 24)]
    [InlineData(2, 32)]
    [InlineData(3, 40)]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    [InlineData(6, 40)]
    [InlineData(7, 52)]
    public void Eight_native_groups_publish_current_projection_and_signed_bias(int group, int packetLength)
    {
        NativeBattleEffectMemory memory = Memory(2000);
        NativeBattleDefaultActMeshStep result = Draw(group, memory: memory);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(packetLength, result.Batch.AllocationBytes);
        uint depth = (group & 1) == 0 ? 499u : 500u;
        Assert.Equal(depth, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(depth - 128, U32(result.Context88, 0x2C));
        Assert.Equal(17 + (((int)depth - 128) >> 2), attempt.Primitive!.Bucket);
        Assert.Equal(-128, unchecked((int)U32(result.Context88, 0x18)));
        Assert.Equal(SelectedAddress, U32(result.Context88, 4));
        Assert.Equal(memory.GeometryRegisters!.Rotation20, result.Memory.GeometryRegisters.Rotation20);
        Assert.Equal(memory.GeometryRegisters.Translation, result.Memory.GeometryRegisters.Translation);
        Assert.Equal(Address + (uint)Source(group).Data.Length, U32(result.Context88, 0x20));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x34, 4));
    }

    [Fact]
    public void GT3_uses_current_DPCT_full_cue_and_RGB_three_bytes_only()
    {
        NativeBattleEffectArena context = Context(0x20B2).Write(12, BitConverter.GetBytes(0x12340800u));
        NativeBattleDefaultActMeshStep result = Draw(6, context);
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(0x12340800u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(0x3E112233u, result.Memory.ScriptedColorRegisters.Material);
        Assert.Equal(0x120u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0x340u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0x560u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(11, 1));
        Assert.Equal(3, result.Memory.ScriptedColorRegisters.Rgb.Length);
    }

    [Fact]
    public void Near_rejection_retains_only_prefix_and_never_reads_fourth_vertex_or_depth_bias()
    {
        List<int> reads = [];
        NativeBattleDefaultActMeshStep result = Draw(7, Context().MarkUnknown(0x18, 4), Memory(0), offset =>
        {
            reads.Add(offset);
            return Vertex(offset);
        });
        Assert.Equal(new[] { 0, 8, 16 }, reads);
        Assert.Equal(0, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x24, 4));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
        Assert.Null(result.Memory.GeometryRegisters!.Otz);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Screen_rejected_GT3_executes_AVSZ_but_preserves_context_depth_and_color_FIFO()
    {
        NativeBattleDefaultActMeshStep result = Draw(6, projection: new(null, 100, -100, -100));
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(499u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(499u, result.Memory.GeometryRegisters.Mac0);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Theory]
    [InlineData(-1000, 0)]
    [InlineData(2147483647, 0)]
    [InlineData(128, 627)]
    public void Depth_bias_uses_wrapping_signed_DWORD_add_then_clamp(int bias, uint expected)
    {
        NativeBattleDefaultActMeshStep result = Draw(6, Context().Write(0x18, BitConverter.GetBytes(bias)));
        Assert.Equal(expected, U32(result.Context88, 0x2C));
        Assert.Equal(17 + ((int)expected >> 2), Assert.Single(result.Batch.Attempts).Primitive!.Bucket);
    }

    [Fact]
    public void Unknown_selected_W_and_rotation_padding_survive_JSON_and_next_draw()
    {
        NativeBattleDefaultActMeshStep unknown = Draw(7);
        NativeBattleDefaultActMeshStep known = Draw(7, readW: _ => 1234);
        Assert.Equal(NativeBattleTestSnapshots.Json(known.Batch), NativeBattleTestSnapshots.Json(unknown.Batch));
        Assert.False(unknown.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Null(unknown.Memory.GeometryRegisters!.Rotation20[18]);
        NativeBattleDefaultActMeshStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDefaultActMeshStep>(NativeBattleTestSnapshots.Json(unknown))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Draw(7, unknown.Context88, unknown.Memory)), NativeBattleTestSnapshots.Json(Draw(7, restored.Context88, restored.Memory)));
    }

    [Fact]
    public void Source_vertices_replace_ctx4_only_without_preserved_vertex_flag()
    {
        NativeBattleGfAnimationSource source = Source(6, packed: true);
        NativeBattleDefaultActMeshStep result = NativeBattleDefaultActMesh.Draw(Memory(2000), source, 0, Context(0x30), Projection, Constants);
        Assert.Equal(Address + 8, U32(result.Context88, 4));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal((short)0x3333, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.GeometryRegisters!.V2.Select(value => value!.Value).ToArray().AsSpan(6)));
        Assert.Throws<NotSupportedException>(() => NativeBattleDefaultActMesh.Draw(Memory(2000), source, 0, Context(), Projection, Constants));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Group12_capture_uses_loaded_matrix_and_exact_inline_selected_or_packed_operands_without_native_changes(int group)
    {
        for (int mode = 0; mode < 3; mode++)
        {
            NativeBattleGfAnimationSource source = Source(group, packed: true);
            NativeBattleActionMatrix loaded = new([8192, 0, 0, 0, 4096, 0, 0, 0, 4096], 70, 30, 2100, 4096);
            NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { X = 50, Y = 10, Z = 100 };
            NativeBattleCameraProjection projection = new(view, 100, 160, 108);
            NativeBattleEffectMemory memory = Memory(2000);
            memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters!, loaded, true, true, null) };
            NativeBattleEffectArena context = Context(mode == 0 ? 0x30u : 0x2030u);
            NativeBattlePilotVisualIdentity identity = new(138, 0x1000, 2, 7, 19);
            List<int> reads = [];
            NativeBattleDefaultActMeshStep Draw(NativeBattlePilotVisualIdentity? capture)
            {
                return NativeBattleDefaultActMesh.Draw(memory, source, 0, context, projection, Constants, ReadVertex, _ => null, readPackedVertex: mode == 2 ? ReadPacked : null, pilotIdentity: capture);
            }
            NativeBattleVector ReadVertex(int offset)
            {
                reads.Add(offset);
                NativeBattleVector vertex = Vertex(offset);
                return vertex with { X = (short)(vertex.X + 25) };
            }
            NativeBattleEffectArena ReadPacked(int offset)
            {
                NativeBattleVector vertex = ReadVertex(offset);
                byte[] bytes = new byte[8];
                BinaryPrimitives.WriteInt16LittleEndian(bytes, vertex.X);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), vertex.Y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), vertex.Z);
                return NativeBattleEffectArena.Clear().Write(0, bytes).MarkUnknown(6, 2);
            }
            NativeBattleDefaultActMeshStep plain = Draw(null);
            int[] originalReads = reads.ToArray();
            reads.Clear();
            NativeBattleDefaultActMeshStep captured = Draw(identity);
            Assert.Equal(originalReads, reads);
            Assert.Equal(NativeBattleTestSnapshots.Json(plain.Memory), NativeBattleTestSnapshots.Json(captured.Memory));
            Assert.Equal(NativeBattleTestSnapshots.Json(plain.Context88), NativeBattleTestSnapshots.Json(captured.Context88));
            NativeBattleGfGeometryAttempt expected = Assert.Single(plain.Batch.Attempts);
            NativeBattleGfGeometryAttempt actual = Assert.Single(captured.Batch.Attempts);
            Assert.Equal(expected.Data.ToArray(), actual.Data.ToArray());
            Assert.Equal(expected.WriteMask.ToArray(), actual.WriteMask.ToArray());
            Assert.Equal(plain.Batch.AllocationBytes, captured.Batch.AllocationBytes);
            Assert.Null(expected.Primitive!.PilotGeometry);
            Assert.Equal(NativeBattleTestSnapshots.Json(expected.Primitive), NativeBattleTestSnapshots.Json(actual.Primitive! with { PilotGeometry = null }));
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.Primitive.PilotGeometry);
            Assert.Equal(identity.Bank, geometry.Identity.Bank);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal(new Vector3(mode == 0 ? -780 : -730, mode == 2 ? 420 : -380, 2000), geometry.A);
            Assert.Equal(new Vector3(mode == 0 ? 820 : 870, -380, 2000), geometry.B);
            Assert.Equal(new Vector3(mode == 0 ? -780 : -730, 420, 2000), geometry.C);
            if (mode == 2)
            {
                Assert.Equal(geometry.C, geometry.A);
            }
            Assert.Equal((byte)((group & 1) == 0 ? 3 : 4), geometry.CornerCount);
            if ((group & 1) != 0)
            {
                Assert.Equal(new Vector3(mode == 0 ? 820 : 870, 420, 2000), geometry.D);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Group12_capture_preserves_native_fourth_reachability_after_near_or_backface_checks(bool backface)
    {
        NativeBattleGfAnimationSource source = Source(7);
        byte[] bytes = source.Data.ToArray();
        const int face = 12 + 4 + 7 * 4;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0xFFFE);
        if (backface)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 4);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 2);
        }
        source = source with { Data = bytes.ToImmutableArray() };
        foreach (bool packed in new[] { false, true })
        {
            List<int> reads = [];
            NativeBattleVector ReadVertex(int offset)
            {
                reads.Add(offset);
                return Vertex(offset);
            }
            NativeBattleEffectArena ReadPacked(int offset)
            {
                NativeBattleVector vertex = ReadVertex(offset);
                byte[] row = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(row, vertex.X);
                BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(2), vertex.Y);
                BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(4), vertex.Z);
                return NativeBattleEffectArena.Clear().Write(0, row).MarkUnknown(6, 2);
            }
            NativeBattleDefaultActMeshStep DrawNative(NativeBattlePilotVisualIdentity? identity) => NativeBattleDefaultActMesh.Draw(Memory(backface ? 2000 : 0), source, 0, Context(0x2000).MarkUnknown(0x18, 4), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), Constants, ReadVertex, _ => null, readPackedVertex: packed ? ReadPacked : null, pilotIdentity: identity);
            if (backface && packed)
            {
                // Packed snapshots overwrite the retained V0 NormalInput on each native load.
                // The reached first-three projection is degenerate, so native NCLIP does not reject it.
                InvalidDataException baseline = Assert.Throws<InvalidDataException>(() => DrawNative(null));
                Assert.Equal(new[] { 0, 16, 8, 0xFFFE * 4 }, reads);
                reads.Clear();
                InvalidDataException captured = Assert.Throws<InvalidDataException>(() => DrawNative(new(138, 0x1000, 2, 7, 0)));
                Assert.Equal(baseline.Message, captured.Message);
                Assert.Equal(new[] { 0, 16, 8, 0xFFFE * 4 }, reads);
                continue;
            }
            NativeBattleDefaultActMeshStep plain = DrawNative(null);
            int[] baselineReads = reads.ToArray();
            reads.Clear();
            NativeBattleDefaultActMeshStep result = DrawNative(new(138, 0x1000, 2, 7, 0));
            Assert.Equal(baselineReads, reads);
            Assert.Equal(backface ? new[] { 0, 16, 8 } : new[] { 0, 8, 16 }, reads);
            Assert.Equal(NativeBattleTestSnapshots.Json(plain.Memory), NativeBattleTestSnapshots.Json(result.Memory));
            Assert.Equal(NativeBattleTestSnapshots.Json(plain.Context88), NativeBattleTestSnapshots.Json(result.Context88));
            Assert.Equal(NativeBattleTestSnapshots.Json(plain.Batch), NativeBattleTestSnapshots.Json(result.Batch));
            Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
            Assert.Equal(0, result.Batch.AllocationBytes);
            Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
        }
    }

    private const uint Address = 0x112015C;
    private const uint SelectedAddress = 0x20E19B8;
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private static NativeBattleDefaultActMeshStep Draw(int group, NativeBattleEffectArena? context = null, NativeBattleEffectMemory? memory = null, Func<int, NativeBattleVector>? readVertex = null, Func<int, short?>? readW = null, NativeBattleCameraProjection? projection = null) => NativeBattleDefaultActMesh.Draw(memory ?? Memory(2000), Source(group), 0, context ?? Context(), projection ?? Projection, Constants, readVertex ?? Vertex, readW ?? (_ => null));
    private static NativeBattleEffectMemory Memory(int z)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }
    private static NativeBattleEffectArena Context(uint flags = 0x2030) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address)).Write(4, BitConverter.GetBytes(SelectedAddress)).Write(8, [0x12, 0x34, 0x56]).Write(12, BitConverter.GetBytes(2048)).Write(0x18, BitConverter.GetBytes(-128)).Write(0x1C, BitConverter.GetBytes(flags));
    private static NativeBattleVector Vertex(int offset) => offset switch { 0 => new(-400, -400, 0), 8 => new(400, -400, 0), 16 => new(-400, 400, 0), 24 => new(400, 400, 0), _ => throw new InvalidDataException("Unexpected selected vertex offset.") };
    private static uint U32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    private static NativeBattleGfAnimationSource Source(int group, bool packed = false)
    {
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        int directory = packed ? 40 : 12;
        byte[] bytes = new byte[directory + 32 + lengths[group]];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, directory);
        if (packed)
        {
            for (int index = 0; index < 4; index++)
            {
                NativeBattleVector vertex = Vertex(index * 8);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), vertex.X);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), vertex.Y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), vertex.Z);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), unchecked((short)((index + 1) * 0x1111)));
            }
        }
        int face = directory + 4 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        uint[] commands = [0x20000000, 0x28000000, 0x24000000, 0x2C000000, 0x30000000, 0x38000000, 0x3C000000, 0x3C000000];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), commands[group] | 0x112233);
        for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        if (group is 2 or 3 or 6 or 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x9ABCDEF0);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x76543210);
            }
        }
        if (group >= 4)
        {
            int colors = face + (group >= 6 ? ((group & 1) != 0 ? 24 : 20) : 12);
            for (int index = 0; index < ((group & 1) != 0 ? 3 : 2); index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(colors + index * 4), 0x30224466u + (uint)index * 0x00112233);
            }
        }
        return new(Address, bytes.ToImmutableArray());
    }
}
