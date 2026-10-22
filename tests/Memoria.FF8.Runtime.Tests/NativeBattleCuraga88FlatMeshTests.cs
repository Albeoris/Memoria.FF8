using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuraga88FlatMeshTests
{
    private const uint Address = 0x10490A8;
    private static NativeBattleActionMatrix Matrix => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Theory]
    [InlineData(0, 20, 0x04000000u, 499u)]
    [InlineData(1, 24, 0x05000000u, 500u)]
    [InlineData(2, 32, 0x07000000u, 499u)]
    public void Flat_leaves_publish_actual_projection_average_context_and_private_packet_extent(int group, int packetLength, uint tag, uint depth)
    {
        ImmutableArray<byte> mesh = Mesh(group);
        NativeBattleCuraga88MeshStep result = Emit(mesh);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(packetLength, result.Batch.AllocationBytes);
        Assert.Equal(tag, attempt.Primitive.Words[0]);
        Assert.Equal(depth, result.Registers.Geometry.Otz);
        Assert.Equal(depth, U32(result.Context88, 0x2C));
        Assert.Equal(17 + ((int)depth >> 2), attempt.Primitive.Bucket);
        Assert.Equal(Address + (uint)mesh.Length, U32(result.Context88, 0x20));
        Assert.Equal(102400u, U32(result.Context88, 0x24));
        Assert.Equal(0u, U32(result.Context88, 0x30) & 0x60000);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Equal(new uint?[] { null, 0x12345678, 0xABCDEF01 }, result.Registers.Colors.Rgb);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x28, 4));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(8, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Flat_cull_uses_bit10_and_rejects_before_average_or_fourth_projection(int group)
    {
        List<int> reads = [];
        short? ReadW(int offset)
        {
            reads.Add(offset);
            return null;
        }
        NativeBattleCuraga88MeshStep rejected = Emit(Mesh(group, reverse: true), Context(0x20), zsf3: null, zsf4: null, readW: ReadW);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(rejected.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, rejected.Batch.AllocationBytes);
        Assert.Null(rejected.Registers.Geometry.Otz);
        Assert.Equal(new[] { 8, 24, 16 }, reads);
        Assert.All(attempt.WriteMask.Take(8), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => rejected.Context88.Read(0x2C, 4));
        Assert.True(unchecked((int)U32(rejected.Context88, 0x24)) < 0);
    }

    [Theory]
    [InlineData(0, 499u)]
    [InlineData(1, 500u)]
    [InlineData(2, 499u)]
    public void Screen_rejection_retains_reached_average_but_never_publishes_depth_or_shades(int group, uint depth)
    {
        NativeBattleCuraga88MeshStep result = Emit(Mesh(group), projection: new(null, 100, -100, -100));
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(depth, result.Registers.Geometry.Otz);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Accepted_flat_fade_publishes_full_DPCS_operands_and_preserves_JSON_next_state(int group)
    {
        NativeBattleEffectArena context = Dword(Context(0x51), 8, 0xC08040);
        context = Dword(context, 12, 0x12340800);
        NativeBattleCuraga88MeshStep result = Emit(Mesh(group), context);
        uint expectedMaterial = (group switch { 0 => 0x20000000u, 1 => 0x28000000u, _ => 0x24000000u }) | 0x02332211;
        Assert.Equal(expectedMaterial, result.Registers.Colors.Material);
        Assert.Equal(0x12340800u, result.Registers.Colors.Ir0);
        Assert.Equal(new int?[] { 648, 1296, 1944 }, result.Registers.Colors.Mac);
        Assert.Equal(new int?[] { 648, 1296, 1944 }, result.Registers.Colors.Ir);
        Assert.Equal(result.Registers.Colors.Rgb[2], Assert.Single(result.Batch.Attempts).Primitive!.Words[1]);
        NativeBattleCuraga88MeshStep restored = JsonSerializer.Deserialize<NativeBattleCuraga88MeshStep>(JsonSerializer.Serialize(result))!;
        NativeBattleCuraga88MeshStep Next(NativeBattleCuraga88MeshStep retained)
        {
            return NativeBattleCuraga88Mesh.EmitBatch(Mesh(group), Address, Matrix, new(null, 100, 160, 108), retained.Context88, retained.Registers, retained.FarControls, 0, 0, 0x100, Constants, null, 0x155);
        }
        Assert.Equal(JsonSerializer.Serialize(Next(result)), JsonSerializer.Serialize(Next(restored)));
    }

    [Fact]
    public void FT3_material_add_and_replace_keep_native_UV_DWORD_carries_and_all_prefix_stores()
    {
        NativeBattleEffectArena context = Dword(Context(0x1F30), 0x18, 0x00010002);
        context = context.Write(0x10, [0x10, 0]).Write(0x14, [0x20, 0]);
        NativeBattleCuraga88MeshStep result = Emit(Mesh(2), context);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal(0x12550304u, attempt.Primitive!.Words[3]);
        Assert.Equal(0x56890607u, attempt.Primitive.Words[5]);
        Assert.Equal(0x00010A0Bu, attempt.Primitive.Words[7]);
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Fact]
    public void F3_uses_current_signed_ZSF3_instead_of_a_fixed_depth_formula()
    {
        NativeBattleCuraga88MeshStep result = Emit(Mesh(0), zsf3: -1);
        Assert.Equal(1048574u, result.Registers.Geometry.Otz);
        Assert.Equal(17 + (1048574 >> 2), Assert.Single(result.Batch.Attempts).Primitive!.Bucket);
        Assert.Throws<NotSupportedException>(() => Emit(Mesh(0), zsf3: null));
    }

    private static NativeBattleCuraga88MeshStep Emit(ImmutableArray<byte> mesh, NativeBattleEffectArena? context = null, NativeBattleCameraProjection? projection = null, short? zsf3 = 0x155, short? zsf4 = 0x100, Func<int, short?>? readW = null)
    {
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [null, 0x12345678, 0xABCDEF01] };
        return NativeBattleCuraga88Mesh.EmitBatch(mesh, Address, Matrix, projection ?? new(null, 100, 160, 108), context ?? Context(0x30), new(NativeBattleGeometryRegisters.Unknown(), colors), new(1, 2, 3, 4, 5), 0, 0, zsf4, Constants, null, zsf3, readW);
    }

    private static NativeBattleEffectArena Context(uint flags)
    {
        return Dword(NativeBattleCuraga88Mesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), Address, 0, 0, null), 0x1C, flags);
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        return arena.Write(offset, BitConverter.GetBytes(value));
    }

    private static uint U32(NativeBattleEffectArena arena, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    }

    private static ImmutableArray<byte> Mesh(int group, bool reverse = false)
    {
        int stride = group == 2 ? 20 : 12;
        byte[] mesh = new byte[72 + stride];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 4);
        short[] positions = [-400, -400, 0, 0x1111, 400, -400, 0, 0x2222, -400, 400, 0, 0x3333, 400, 400, 0, 0x4444];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 2), positions[index]);
        }
        int at = 40 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(at), 1);
        at += 4;
        uint code = group switch { 0 => 0x20000000u, 1 => 0x28000000u, _ => 0x24000000u };
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at), code | 0x332211);
        ushort[] offsets = reverse ? [0, 4, 2, 6] : [0, 2, 4, 6];
        for (int index = 0; index < (group == 1 ? 4 : 3); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(at + 4 + index * 2), offsets[index]);
        }
        if (group == 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(at + 10), 0x0A09);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 12), 0x12340302);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 16), 0x56780605);
        }
        return mesh.ToImmutableArray();
    }
}
