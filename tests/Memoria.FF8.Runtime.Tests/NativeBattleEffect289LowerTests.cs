using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect289LowerTests
{
    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 52)]
    public void Current_first_vertex_W_alone_controls_fade_and_zero_area_is_accepted(bool gouraud, int length)
    {
        NativeBattleStaticMeshResource mesh = Mesh(gouraud);
        NativeBattleEffect289CustomMesh108Step result = Draw(mesh, Context(), offset => (new NativeBattleVector(0, 0, 0), offset == 0 ? (short)0 : (short)4096));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(length, result.Batch.AllocationBytes);
        Assert.Equal(gouraud ? 0x3E112233u : 0x2E112233u, primitive.Words[1]);
        Assert.Equal(142, primitive.Bucket);
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
        Assert.Equal(0u, U(result.Context108, 0x38));
        Assert.Equal(500u, U(result.Context108, 0x40));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(result.Context108.Read(0x4E, 2)));
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void First_W_cutoff_skips_all_projection_and_packet_stores()
    {
        NativeBattleEffectMemory initial = Memory();
        NativeBattleStaticMeshResource mesh = Mesh(false);
        int reads = 0;
        NativeBattleEffect289CustomMesh108Step result = NativeBattleEffect289CustomMesh108.DrawLoaded(initial, mesh, new(null, 100, 160, 108), Context(), NativeBattleGteColorInterpolation.CodeConstants45F270, offset =>
        {
            reads++;
            return (new(0, 0, 0), (short)4096);
        });
        Assert.Equal(1, reads);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(initial.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Equal(initial.ScriptedColorRegisters, result.Memory.ScriptedColorRegisters);
        Assert.DoesNotContain(true, result.Context108.ReadSnapshot(0x38, 4).Known);
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(result.Context108, 0x34));
    }

    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 52)]
    public void E2_links_keep_physical_polygon_then_modes_and_full_DWORD_U_wrap(bool gouraud, int length)
    {
        NativeBattleEffect289CustomMesh108Step result = Draw(Mesh(gouraud), Context(256), offset => (new NativeBattleVector(0, 0, 0), (short)0));
        Assert.Equal(length + 24, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 0, length, 0, length + 12 }, result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Null(result.Batch.Attempts[0].Primitive);
        NativeBattleGeometryPrimitive polygon = Assert.IsType<NativeBattleGeometryPrimitive>(result.Batch.Attempts[2].Primitive);
        Assert.Equal((byte)138, unchecked((byte)polygon.Words[3]));
        Assert.Equal(0x00140000u, Word(result.Batch.Attempts[0], 12) & 0xFFFFFF00u);
        Assert.Equal(0xE2000000u, result.Batch.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(0xE2000010u, result.Batch.Attempts[3].Primitive!.Words[1]);
        Assert.All(result.Batch.Attempts[2].WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal(new[] { 0, 0, 0, 0 }, result.Batch.Attempts[1].Primitive!.VertexPayload4);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result));
        NativeBattleEffect289CustomMesh108Step restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect289CustomMesh108Step>(json)!);
        Assert.Equal(result.Batch.AllocationBytes, restored.Batch.AllocationBytes);
        Assert.Equal(result.Context108.Read(0x34, 4), restored.Context108.Read(0x34, 4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Current_W_fade_updates_RGB_FIFO_from_the_same_source_color(bool gouraud)
    {
        NativeBattleEffect289CustomMesh108Step result = Draw(Mesh(gouraud), Context(), offset => (new NativeBattleVector(0, 0, 0), (short)2048));
        NativeBattleGeometryPrimitive polygon = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(gouraud ? 0x3E081119u : 0x2E081119u, polygon.Words[1]);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(gouraud ? 0x3E112233u : 0x2E112233u, result.Memory.ScriptedColorRegisters.Material);
        Assert.Equal(new int?[] { 408, 272, 136 }, result.Memory.ScriptedColorRegisters.Mac.ToArray());
    }

    [Fact]
    public void Warp_uses_current_W_lookup_order_wrap_arithmetic_and_preserves_T_and_adjacent_bytes()
    {
        byte[] bytes = Mesh(false).Bytes.ToArray();
        for (int index = 0; index < 164; index++)
        {
            int offset = 8 + index * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 100);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), 200);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), 300);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 6), 20);
        }
        List<int> lookups = [];
        NativeBattleEffectMemory initial = Memory() with { Arena = NativeBattleEffectArena.Clear().Write(0x151F, new byte[] { 0xA5 }).Write(0x1A40, new byte[] { 0x5A }) };
        NativeBattleEffectMemory result = NativeBattleEffect289WarpVertices.Execute6790E0(initial, new(0, 0x10000000, bytes.ToImmutableArray()), index =>
        {
            lookups.Add(index);
            return (short)index;
        }, 0, 4096, 2048, 24, 2048, 0x20DFAB8u + 0x1520);
        Assert.Equal(Enumerable.Range(0, 164).SelectMany(_ => new[] { 20, 10 }), lookups);
        Assert.Equal(new short[] { 50, 80, 300, 4 }, Enumerable.Range(0, 4).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(result.Arena!.Read(0x1520 + axis * 2, 2))));
        Assert.Equal(0xA5, result.Arena!.Read(0x151F, 1)[0]);
        Assert.Equal(0x5A, result.Arena.Read(0x1A40, 1)[0]);
        Assert.Equal(initial.GeometryRegisters!.Translation, result.GeometryRegisters!.Translation);
        Assert.Equal(new int?[] { 50, 100, 300 }, result.ScriptedColorRegisters!.Ir.ToArray());
    }

    [Fact]
    public void Unwritten_context_bytes_stay_unknown_until_a_reached_native_read_requires_them()
    {
        NativeBattleEffectArena unknownFlags = Context().MarkUnknown(0x1C, 4);
        NativeBattleEffect289CustomMesh108Step flat = Draw(Mesh(false), unknownFlags, offset => (new NativeBattleVector(0, 0, 0), (short)0));
        Assert.Single(flat.Batch.Attempts);
        Assert.DoesNotContain(true, flat.Context108.ReadSnapshot(0x18, 4).Known);
        Assert.DoesNotContain(true, flat.Context108.ReadSnapshot(0x22, 2).Known);
        Assert.DoesNotContain(true, flat.Context108.ReadSnapshot(0x1C, 4).Known);
        Assert.Throws<NotSupportedException>(() => Draw(Mesh(true), unknownFlags, offset => (new NativeBattleVector(0, 0, 0), (short)0)));
        Assert.Throws<NotSupportedException>(() => Draw(Mesh(false), Context(), offset => (new NativeBattleVector(0, 0, 0), null)));
    }
    private static NativeBattleEffect289CustomMesh108Step Draw(NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context, Func<int, (NativeBattleVector Vertex, short? W)> vertices) => NativeBattleEffect289CustomMesh108.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, vertices);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(ushort u = 0) => NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(0x10000000u)).Write(4, BitConverter.GetBytes(0x20E0FD8u)).Write(8, new byte[4]).Write(0x1C, new byte[] { 0x30 }).Write(0x20, BitConverter.GetBytes(u)).Write(0x24, new byte[4]).Write(0x28, BitConverter.GetBytes(0x01000100u)).Write(0x2C, new byte[4]).Write(0x30, BitConverter.GetBytes(0x01000080u));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));

    private static NativeBattleStaticMeshResource Mesh(bool gouraud)
    {
        int length = gouraud ? 36 : 24;
        byte[] bytes = new byte[1320 + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1320);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 164);
        int cursor = 1320;
        for (int group = 0; group < 8; group++)
        {
            bool selected = group == (gouraud ? 7 : 3);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), selected ? 1 : 0);
            cursor += 4;
            if (selected)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), gouraud ? 0x3E112233u : 0x2E112233u);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 6), 2);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 8), 4);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 10), 6);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x0014000A);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 16), 0x0028001E);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 20), 0x003C0032);
                if (gouraud)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 24), 0x00224466);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 28), 0x00446688);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 32), 0x006688AA);
                }
                cursor += length;
            }
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
