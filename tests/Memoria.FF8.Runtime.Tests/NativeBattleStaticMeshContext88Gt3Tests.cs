using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStaticMeshContext88Gt3Tests
{
    [Fact]
    public void GT3_retains_native_40_byte_packet_UVs_and_partial_RGB_with_actual_GTE_depth()
    {
        NativeBattleStaticMeshResource mesh = Mesh(6);
        NativeBattleStaticMeshContext88Step drawn = Draw(mesh, Context(0));
        NativeBattleGfGeometryAttempt packet = Assert.Single(drawn.Batch.Attempts);
        Assert.Equal(40, packet.PacketLength);
        Assert.Equal(40, drawn.Batch.AllocationBytes);
        Assert.Equal(0x09000000u, packet.Primitive!.Words[0]);
        Assert.Equal(0x3C112233u, packet.Primitive.Words[1]);
        Assert.Equal(0x12345678u, packet.Primitive.Words[3]);
        Assert.Equal(0x9ABCDEF0u, packet.Primitive.Words[6]);
        Assert.Equal(0x7654u, packet.Primitive.Words[9]);
        Assert.Equal(499u, drawn.Memory.GeometryRegisters!.Otz);
        Assert.Equal(141, packet.Primitive.Bucket);
        Assert.Equal(0x120u, drawn.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0x340u, drawn.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0x560u, drawn.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.Throws<NotSupportedException>(() => drawn.Context.Read(11, 1));
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U32(drawn.Context, 0x20));
        Assert.Null(drawn.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void GT3_near_rejection_retains_native_packet_prefix_without_reading_cue_or_shading()
    {
        NativeBattleStaticMeshContext88Step drawn = Draw(Mesh(6), Context(0xC3).MarkUnknown(12, 4), z: 0);
        NativeBattleGfGeometryAttempt packet = Assert.Single(drawn.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0, drawn.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[24]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[36]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => drawn.Context.Read(0x24, 4));
        Assert.Throws<NotSupportedException>(() => drawn.Context.Read(0x2C, 4));
        Assert.Null(drawn.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void GT3_culls_negative_area_without_bit20_but_keeps_native_zero_area_admission()
    {
        NativeBattleStaticMeshResource mesh = Mesh(6);
        byte[] bytes = mesh.Bytes.ToArray();
        int face = Face(6);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 2);
        mesh = mesh with { Bytes = bytes.ToImmutableArray() };
        NativeBattleStaticMeshContext88Step rejected = Draw(mesh, Context(0));
        Assert.Null(Assert.Single(rejected.Batch.Attempts).Primitive);
        Assert.True(unchecked((int)U32(rejected.Context, 0x24)) < 0);
        Assert.Throws<NotSupportedException>(() => rejected.Context.Read(0x2C, 4));
        Assert.NotNull(Assert.Single(Draw(mesh, Context(0x30)).Batch.Attempts).Primitive);
        Array.Clear(bytes, face + 4, 6);
        NativeBattleStaticMeshContext88Step zero = Draw(mesh with { Bytes = bytes.ToImmutableArray() }, Context(0));
        Assert.NotNull(Assert.Single(zero.Batch.Attempts).Primitive);
        Assert.Equal(0u, U32(zero.Context, 0x24));
    }

    [Fact]
    public void C3_GT3_reads_only_actual_color_words20_24_and_publishes_DPCT_FIFO_in_native_order()
    {
        NativeBattleEffectArena context = Context(0xC3).Write(8, [0, 0, 0]);
        NativeBattleStaticMeshContext88Step drawn = Draw(Mesh(6), context);
        NativeBattleGeometryPrimitive primitive = Assert.Single(drawn.Batch.Attempts).Primitive!;
        Assert.Equal(0x3E081119u, primitive.Words[1]);
        Assert.Equal(0x3E112233u, primitive.Words[4]);
        Assert.Equal(0x3E334455u, primitive.Words[7]);
        Assert.Equal(2048u, drawn.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(0x3E112233u, drawn.Memory.ScriptedColorRegisters.Material);
        Assert.Equal(new uint?[] { 0x3E112233, 0x3E334455, 0x3E081119 }, drawn.Memory.ScriptedColorRegisters.Rgb.ToArray());
        Assert.Throws<NotSupportedException>(() => Draw(Mesh(6), context.MarkUnknown(12, 4)));
        Assert.Throws<NotSupportedException>(() => drawn.Context.Read(11, 1));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void C0_untextured_Gouraud_uses_native_bit80_and_retains_exact_material_and_FIFO(int group)
    {
        NativeBattleStaticMeshContext88Step drawn = Draw(Mesh(group), Context(0xC0).Write(8, [0, 0, 0]));
        uint command = group == 4 ? 0x30000000u : 0x38000000u;
        Assert.Equal(command | 0x081119u, Assert.Single(drawn.Batch.Attempts).Primitive!.Words[1]);
        Assert.Equal(2048u, drawn.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(command | 0x081119u, drawn.Memory.ScriptedColorRegisters.Rgb[2]);
        Assert.NotNull(drawn.Memory.ScriptedColorRegisters.Material);
    }

    [Fact]
    public void Accepted_GT3_respects_current_ZSF3_and_JSON_next_with_unknown_RGB_high_byte()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { ScriptedGraphics = memory.ScriptedGraphics! with { Zsf3 = 0x100 } };
        NativeBattleStaticMeshContext88Step drawn = Draw(Mesh(6), Context(0xC3), memory);
        Assert.Equal(375u, drawn.Memory.GeometryRegisters!.Otz);
        Assert.Equal(110, Assert.Single(drawn.Batch.Attempts).Primitive!.Bucket);
        NativeBattleStaticMeshContext88Step restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleStaticMeshContext88Step>(NativeBattleTestSnapshots.Json(drawn))!;
        NativeBattleStaticMeshContext88Step Next(NativeBattleStaticMeshContext88Step current) => Draw(Mesh(6), current.Context, current.Memory);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(drawn)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    [Fact]
    public void Unfaded_GT3_preserves_unknown_FAR_lane_until_depth_cue_consumes_it()
    {
        NativeBattleStaticMeshContext88Step drawn = Draw(Mesh(6), Context(0).MarkUnknown(9, 1));
        Assert.Equal(Assert.Single(Draw(Mesh(6), Context(0)).Batch.Attempts).Primitive!.Words, Assert.Single(drawn.Batch.Attempts).Primitive!.Words);
        Assert.Equal(0x120u, drawn.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Null(drawn.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0x560u, drawn.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.Throws<NotSupportedException>(() => drawn.Context.Read(9, 1));
        NativeBattleStaticMeshContext88Step restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleStaticMeshContext88Step>(NativeBattleTestSnapshots.Json(drawn))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Draw(Mesh(6), drawn.Context, drawn.Memory)), NativeBattleTestSnapshots.Json(Draw(Mesh(6), restored.Context, restored.Memory)));
        Assert.Throws<NotSupportedException>(() => Draw(Mesh(6), Context(0xC3).MarkUnknown(9, 1)));
    }

    private const uint Address = 0xCE2CE4;
    private static NativeBattleStaticMeshContext88Step Draw(NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context, NativeBattleEffectMemory? memory = null, int z = 2000) => NativeBattleStaticMeshContext88.Draw(memory ?? Memory(), mesh, NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address)).Write(8, [0x12, 0x34, 0x56]).Write(12, BitConverter.GetBytes(2048)).Write(0x1C, BitConverter.GetBytes(flags));
    private static uint U32(NativeBattleEffectArena arena, int at) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(at, 4));
    private static int Face(int group) => 44 + group * 4;
    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int stride = group == 6 ? 28 : group == 5 ? 24 : 20;
        byte[] bytes = new byte[72 + stride];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] positions = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), positions[index]);
        }
        int face = Face(group);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        uint command = group == 6 ? 0x3C000000u : group == 5 ? 0x38000000u : 0x30000000u;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), command | 0x112233);
        for (int corner = 0; corner < (group == 5 ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        int color = group == 6 ? 20 : 12;
        if (group == 6)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0x7654);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x9ABCDEF0);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + color), 0x30224466);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + color + 4), 0x306688AA);
        if (group == 5)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + color + 8), 0x30AACCEE);
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
