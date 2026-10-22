using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid317ModelDrawingTests
{
    [Fact]
    public void Deformation_reads_unsigned_cache_and_preserves_unwritten_W_before_projection()
    {
        NativeBattleAcid317ModelStep result = Draw(Geometry(true), Context(1024), Memory(255), 2000);
        uint row = U32(result.VertexScratch, 0);
        Assert.Equal((short)1120, unchecked((short)row));
        Assert.Equal((short)806, unchecked((short)(row >> 16)));
        Assert.Equal(500u, U32(result.VertexScratch, 4));
        Assert.Equal(CacheAddress + 4, U32(result.Temporary132, 0x2C));
        Assert.Equal(ScratchAddress + 32, U32(result.Temporary132, 0x28));
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal(0xA5A5A5A5u, U32(result.Temporary132, 0x1C));
        Assert.Equal((ushort)0xA5A5, BinaryPrimitives.ReadUInt16LittleEndian(result.Temporary132.Read(0x82, 2)));
    }

    [Fact]
    public void Native_preprojected_FT3_FT4_use_quarter_depth_and_WORD_only_UV_tails()
    {
        NativeBattleAcid317ModelStep result = Draw(Geometry(true), Context(0), Memory(0), 2000);
        Assert.Equal(72, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt tri = result.Batch.Attempts[0];
        NativeBattleGfGeometryAttempt quad = result.Batch.Attempts[1];
        Assert.Equal(0x26112233u, tri.Primitive!.Words[1]);
        Assert.Equal(0x2E112233u, quad.Primitive!.Words[1]);
        Assert.Equal(0xFCDCBA98u, tri.Primitive.Words[5]);
        Assert.Equal(0x8123u, tri.Primitive.Words[7]);
        Assert.Equal(0x6789u, quad.Primitive.Words[7]);
        Assert.Equal(0xF345u, quad.Primitive.Words[9]);
        Assert.Equal(141, tri.Primitive.Bucket);
        Assert.Equal(142, quad.Primitive.Bucket);
        Assert.All(result.Memory.GeometryRegisters!.Sz, value => Assert.Equal(500u, value));
        Assert.Equal(125u, U32(result.Temporary132, 0x78));
        Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(result.Temporary132.Read(0x34, 2)));
        Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(result.Temporary132.Read(0x36, 2)));
        Assert.Equal(GeometryAddress + 52, U32(result.Temporary132, 0x20));
        Assert.Equal(GeometryAddress + 52, U32(result.Temporary132, 0x30));
        Assert.Equal(NativeBattleGfPacketByteState.Known, tri.WriteMask[29]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, tri.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, tri.WriteMask[31]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, quad.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, quad.WriteMask[38]);
    }

    [Fact]
    public void Zero_quarter_depth_writes_only_row_W_and_rejects_before_NCLIP_or_packet_stores()
    {
        NativeBattleAcid317ModelStep result = Draw(Geometry(false), Context(1024), Memory(255), 0);
        Assert.Equal(new short[] { -400, -145, 0, 16 }, Words(result.VertexScratch.Read(0, 8)));
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.All(result.Batch.Attempts[0].WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.Equal(0xA5A5A5A5u, U32(result.Temporary132, 0x70));
        Assert.Equal(0xA5A5A5A5u, U32(result.Temporary132, 0x78));
    }

    [Fact]
    public void Negative_area_rejects_but_native_zero_area_is_accepted()
    {
        byte[] reversed = Geometry(false);
        BinaryPrimitives.WriteUInt16LittleEndian(reversed.AsSpan(54), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(reversed.AsSpan(56), 1);
        NativeBattleAcid317ModelStep rejected = Draw(reversed, Context(0), Memory(0), 2000);
        Assert.Null(Assert.Single(rejected.Batch.Attempts).Primitive);
        Assert.True(unchecked((int)U32(rejected.Temporary132, 0x70)) < 0);
        byte[] flat = Geometry(false);
        Array.Clear(flat, 52, 6);
        NativeBattleAcid317ModelStep accepted = Draw(flat, Context(0), Memory(0), 2000);
        Assert.NotNull(Assert.Single(accepted.Batch.Attempts).Primitive);
        Assert.Equal(0u, U32(accepted.Temporary132, 0x70));
    }

    [Fact]
    public void Unselected_object_does_not_read_body_or_cache_and_keeps_the_raw_object_cursor()
    {
        NativeBattleEffectMemory memory = Memory(0) with { Arena = null };
        Assert.Throws<NotSupportedException>(() => Draw(Geometry(false), Context(0), memory, 2000));
        memory = Memory(0);
        NativeBattleAcid317ModelStep result = NativeBattleAcid317ModelDrawing.DrawBody(memory, Context(0).Write(0x4C, BitConverter.GetBytes(0u)), NativeBattleEffectArena.Clear(), Read, GeometryAddress, [], Camera(), Projection, memory.ScriptedGraphics!, ScratchAddress, 0, CacheAddress, 0);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(GeometryAddress + 8, U32(result.Temporary132, 0x30));
        Assert.Equal(CacheAddress, U32(result.Temporary132, 0x2C));
        Assert.Same(memory.GeometryRegisters, result.Memory.GeometryRegisters);
        byte[] Read(int offset, int count) => offset == 0 ? BitConverter.GetBytes(1) : offset == 4 ? BitConverter.GetBytes(8) : throw new InvalidOperationException("Unselected native object was read.");
    }

    [Fact]
    public void Actual_scratch_cache_and_known_byte_extents_are_enforced()
    {
        Assert.Throws<InvalidDataException>(() => Draw(Geometry(false), Context(0), Memory(0), 2000, 24));
        Assert.Throws<InvalidDataException>(() => Draw(Geometry(false), Context(0).Write(0x2C, BitConverter.GetBytes(CacheAddress + 2048)), Memory(0), 2000));
        NativeBattleEffectMemory unknown = Memory(0);
        unknown = unknown with { Arena = unknown.Arena!.MarkUnknown(CacheOffset, 1) };
        Assert.Throws<NotSupportedException>(() => Draw(Geometry(false), Context(0), unknown, 2000));
    }

    [Fact]
    public void Linked_body_keeps_streaming_cache_and_JSON_next_preserves_physical_state()
    {
        NativeBattleEffectMemory memory = Memory(0);
        memory = memory with { Arena = memory.Arena!.Write(CacheOffset + 4, Enumerable.Repeat((byte)255, 4).ToArray()) };
        NativeBattleAcid317ModelStep first = Draw(Geometry(true), Context(1024), memory, 2000);
        NativeBattleAcid317ModelStep restored = JsonSerializer.Deserialize<NativeBattleAcid317ModelStep>(JsonSerializer.Serialize(first))!;
        NativeBattleAcid317ModelStep Next(NativeBattleAcid317ModelStep current) => Draw(Geometry(true), current.Temporary132, current.Memory, 2000, 32, current.VertexScratch);
        NativeBattleAcid317ModelStep second = Next(first);
        Assert.Equal(CacheAddress + 8, U32(second.Temporary132, 0x2C));
        Assert.NotEqual(U32(first.VertexScratch, 0), U32(second.VertexScratch, 0));
        Assert.Equal(JsonSerializer.Serialize(second), JsonSerializer.Serialize(Next(restored)));
    }

    private const uint GeometryAddress = 0x1000000;
    private const uint ScratchAddress = 0x196B000;
    private const int CacheOffset = 0x3F08;
    private const uint CacheAddress = NativeBattleEffectNodeMemory.ArenaAddress + CacheOffset;
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattlePackedMatrix32 Camera() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static NativeBattleEffectMemory Memory(byte coefficient) => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(CacheOffset, Enumerable.Repeat(coefficient, 8).ToArray()), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleAcid317ModelStep Draw(byte[] geometry, NativeBattleEffectArena temporary, NativeBattleEffectMemory memory, int boneZ, int length = 32, NativeBattleEffectArena? scratch = null) => NativeBattleAcid317ModelDrawing.DrawBody(memory, temporary, scratch ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 40), (offset, count) => geometry.AsSpan(offset, count).ToArray(), GeometryAddress, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = boneZ }, null)], Camera(), Projection, memory.ScriptedGraphics!, ScratchAddress, length, CacheAddress, 2048);
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short[] Words(byte[] bytes) => Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToArray();
    private static NativeBattleEffectArena Context(short blend)
    {
        return NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 132).ToArray()).Write(0x24, BitConverter.GetBytes(ScratchAddress)).Write(0x2C, BitConverter.GetBytes(CacheAddress)).Write(0x40, new byte[4]).Write(0x44, BitConverter.GetBytes((short)320)).Write(0x46, BitConverter.GetBytes((short)216)).Write(0x48, BitConverter.GetBytes(0x00112233u)).Write(0x4C, BitConverter.GetBytes(1u)).Write(0x80, BitConverter.GetBytes(blend));
    }
    private static byte[] Geometry(bool quad)
    {
        byte[] data = new byte[quad ? 88 : 68];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 4);
        short[] xy = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 6), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + index * 6), xy[index * 2 + 1]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(42), quad ? (ushort)1 : (ushort)0);
        Face(52, false);
        if (quad)
        {
            Face(68, true);
        }
        return data;

        void Face(int offset, bool four)
        {
            for (int corner = 0; corner < (four ? 4 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + corner * 2), (ushort)(0xF000 | corner));
            }
            if (!four)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 6), 0x8123);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 8), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 12), 0xFEDCBA98);
            if (four)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 16), 0xF3456789);
            }
        }
    }
}
