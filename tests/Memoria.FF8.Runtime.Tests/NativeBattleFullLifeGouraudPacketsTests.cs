using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFullLifeGouraudPacketsTests
{
    private static NativeBattleFullLifeMeshContext Context => new(0x30, null, null, null, null, null, 17, 2);

    [Theory]
    [InlineData(0u, 0x3A808080u)]
    [InlineData(2u, 0x3A808080u)]
    [InlineData(8u, 0x38808080u)]
    [InlineData(10u, 0x38808080u)]
    public void Full_life_G4_uses_native_semi_bits_and_the_same_normalized_Curaga_leaf(uint flags, uint expected)
    {
        NativeBattleFullLifeMeshContext context = Context with { Flags = flags };
        NativeBattleGfGeometryBatch batch = Emit(Corners(), context);
        NativeBattleGfGeometryBatch curaga = NativeBattleCuragaMeshPackets.EmitG4_87FB90(Face(), index => Corners()[index], new(flags, null, null, null, null, null, 17, 2));
        Assert.Equal(JsonSerializer.Serialize(curaga), JsonSerializer.Serialize(batch));
        Assert.Equal(36, batch.AllocationBytes);
        NativeBattleGeometryPrimitive packet = Assert.Single(batch.Attempts).Primitive!;
        Assert.Equal(0x08000000u, packet.Words[0]);
        Assert.Equal(expected, packet.Words[1]);
        Assert.Equal(0xAA202020u, packet.Words[3]);
        Assert.Equal(0xBB606060u, packet.Words[5]);
        Assert.Equal(0xCCC0C0C0u, packet.Words[7]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Rejection_keeps_the_exact_prefix_or_XY_stage_and_defers_secondary_colors_and_fade(int rejection)
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners();
        if (rejection == 0)
        {
            vertices[0] = vertices[0] with { Flags = 0x60000 };
        }
        else if (rejection == 1)
        {
            (vertices[1], vertices[2]) = (vertices[2], vertices[1]);
        }
        else
        {
            vertices = vertices.Select(vertex => vertex with { Screen = XY(-1, 100) }).ToArray();
        }
        NativeBattleModuleProjectedVertex Read(int corner) => corner == 3 && rejection != 2 ? throw new InvalidOperationException("Fourth RTPS executed before admission.") : vertices[corner];
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitG4_885050(Face(), Read, Context with { Flags = rejection == 1 ? 0x90u : 0xB0u }, byteOffset: 80);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(80, attempt.ByteOffsetRelative);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Null(attempt.Primitive);
        Assert.All(attempt.WriteMask.Take(8), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        for (int corner = 0; corner < 4; corner++)
        {
            Assert.Equal(rejection == 2 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8 + corner * 8]);
            if (corner != 0)
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4 + corner * 8]);
            }
        }
        NativeBattleGfGeometryBatch clone = JsonSerializer.Deserialize<NativeBattleGfGeometryBatch>(JsonSerializer.Serialize(batch))!;
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(clone));
    }

    [Fact]
    public void Only_bit20_bypasses_negative_NCLIP_and_the_fourth_RTPS_flags_do_not_reject()
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners();
        (vertices[1], vertices[2]) = (vertices[2], vertices[1]);
        Assert.Null(Assert.Single(Emit(vertices, Context with { Flags = 0x10 }).Attempts).Primitive);
        vertices[3] = vertices[3] with { Flags = 0x80060000 };
        Assert.NotNull(Assert.Single(Emit(vertices, Context with { Flags = 0x20 }).Attempts).Primitive);
    }

    [Fact]
    public void AVSZ4_256_and_proxy_depths_ignore_material_UV_and_unwritten_depth_bias_operands()
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners();
        ushort[] depths = [1, 15, 31, 65535];
        for (int corner = 0; corner < 4; corner++)
        {
            vertices[corner] = vertices[corner] with { Depth = depths[corner] };
        }
        NativeBattleGeometryPrimitive packet = Assert.Single(Emit(vertices, Context with { Flags = 0x1F30 }).Attempts).Primitive!;
        Assert.Equal(1041, packet.Bucket);
        Assert.Equal(new[] { 1, 15, 31, 65535 }, packet.VertexPayload4);
    }

    [Fact]
    public void Fade_uses_original_fourth_color_command_for_secondaries_and_separate_first_color_interpolation()
    {
        NativeBattleSummonColors rules = new(0, 0, 1f / 4096, 4503601774854144d);
        NativeBattleGeometryPrimitive packet = Assert.Single(Emit(Corners(), Context with { Flags = 0xB0, Fade = 2048, FarColor = 0x404040 }, rules).Attempts).Primitive!;
        Assert.Equal(0x3A606060u, packet.Words[1]);
        Assert.Equal(0xCC303030u, packet.Words[3]);
        Assert.Equal(0xCC505050u, packet.Words[5]);
        Assert.Equal(0xCC808080u, packet.Words[7]);
    }

    [Fact]
    public void Accepted_fade_requires_each_actual_owner_instead_of_filling_unwritten_values()
    {
        NativeBattleSummonColors rules = new(0, 0, 1f / 4096, 4503601774854144d);
        Assert.Throws<NotSupportedException>(() => Emit(Corners(), Context with { Flags = 0xB0, FarColor = 0x404040 }, rules));
        Assert.Throws<NotSupportedException>(() => Emit(Corners(), Context with { Flags = 0xB0, Fade = 2048 }, rules));
        Assert.Throws<NotSupportedException>(() => Emit(Corners(), Context with { Flags = 0xB0, Fade = 2048, FarColor = 0x404040 }));
    }

    [Fact]
    public void Rejected_G4_stores_preserve_unknown_secondary_slots_in_the_current_physical_arena()
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners().Select(vertex => vertex with { Screen = XY(-1, 100) }).ToArray();
        NativeBattleGfGeometryBatch batch = Emit(vertices, Context);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(26);
        memory = memory with { Arena = memory.Arena!.MarkUnknown(128, 36) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(128, written.Cursor);
        Assert.Empty(written.Ordering.Packets);
        Assert.Equal(XY(-1, 100), BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena!.Read(136, 4)));
        Assert.Throws<NotSupportedException>(() => written.Memory.Arena!.Read(140, 4));
    }

    private static NativeBattleGfGeometryBatch Emit(NativeBattleModuleProjectedVertex[] vertices, NativeBattleFullLifeMeshContext context, NativeBattleSummonColors? rules = null)
    {
        return NativeBattleRestorationMeshPackets.EmitG4_885050(Face(), index => vertices[index], context, rules);
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(XY(100, 100), 4096, 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0), new(XY(300, 300), 4096, 0)];
    private static uint XY(short x, short y) => unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;

    private static byte[] Face()
    {
        byte[] face = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x3A808080);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0xAA202020);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0xBB606060);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0xCCC0C0C0);
        return face;
    }
}
