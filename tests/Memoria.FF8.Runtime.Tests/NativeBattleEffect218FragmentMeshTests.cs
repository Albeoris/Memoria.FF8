using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218FragmentMeshTests
{
    [Theory]
    [InlineData(2, 32, 0x24)]
    [InlineData(6, 40, 0x34)]
    public void Pivot_subtraction_preserves_original_W_and_emits_the_actual_triangle_packet_family(int group, int length, byte opcode)
    {
        NativeBattleEffect218FragmentMeshStep result = Draw(group, new(10, 20, 30));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(length, result.Batch.AllocationBytes);
        Assert.Equal(length, attempt.PacketLength);
        Assert.Equal(opcode, (byte)(BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)) >> 24));
        Assert.Equal(new short[] { -400, -400, 0, 111 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Context96.Read(0x34 + index * 2, 2))));
        Assert.Equal(new int[] { 2000, 2000, 2000, 0 }, attempt.Primitive.VertexPayload4);
        Assert.Equal((byte)0x4D, result.Memory.GeometryRegisters!.V2[6]);
        Assert.Equal((byte)1, result.Memory.GeometryRegisters.V2[7]);
        Assert.Equal((short)123, BinaryPrimitives.ReadInt16LittleEndian(result.Context96.Read(0x5E, 2)));
        Assert.DoesNotContain(result.Context96.ReadSnapshot(0x4C, 12).Known, known => known);
    }

    [Fact]
    public void Pivot_subtraction_wraps_each_WORD_before_projection_and_rejection_keeps_early_packet_stores()
    {
        NativeBattleEffect218FragmentMeshStep result = Draw(2, new(-20, 0, 0), true);
        Assert.Equal(unchecked((short)(32760 + 20)), BinaryPrimitives.ReadInt16LittleEndian(result.Context96.Read(0x34, 2)));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(0x24112233u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
        Assert.Equal(0x05500102u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(12)));
    }

    private static NativeBattleEffect218FragmentMeshStep Draw(int group, NativeBattleVector origin, bool wrap = false)
    {
        const uint address = 0x30000000;
        int stride = group == 2 ? 20 : 28;
        byte[] bytes = new byte[32 + 32 + stride];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        NativeBattleVector[] points = wrap ? [new(32760, -400, 0), new(32761, -400, 0), new(32762, 400, 0)] : [new(-390, -380, 30), new(410, -380, 30), new(-390, 420, 30)];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), points[index].Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), (short)(111 * (index + 1)));
        }
        int face = 36 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 2 ? 0x26112233u : 0x36112233u);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0xAABB);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x05500102);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x00A00304);
        if (group == 6)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x445566);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x778899);
        }
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 2000, 4096);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, 0), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 96).Write(0, BitConverter.GetBytes(address)).Write(8, new byte[3]).Write(0x1C, BitConverter.GetBytes(0x3Cu)).Write(0x58, BitConverter.GetBytes(origin.X)).Write(0x5A, BitConverter.GetBytes(origin.Y)).Write(0x5C, BitConverter.GetBytes(origin.Z)).Write(0x5E, BitConverter.GetBytes((short)123));
        return NativeBattleEffect218FragmentMesh.Draw635280(memory, new(0, address, bytes.ToImmutableArray()), new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }
}
