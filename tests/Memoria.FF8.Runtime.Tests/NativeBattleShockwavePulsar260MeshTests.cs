using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShockwavePulsar260MeshTests
{
    [Theory]
    [InlineData(1, 24, 40)]
    [InlineData(2, 32, 40)]
    [InlineData(3, 40, 48)]
    [InlineData(5, 36, 52)]
    [InlineData(7, 52, 60)]
    public void Reached_leaves_preserve_physical_polygon_stores_then_native_mode_link_order(int group, int polygonBytes, int allocation)
    {
        NativeBattleShockwavePulsar260MeshStep result = Draw(group);
        Assert.Equal(allocation, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt prefix = result.Batch.Attempts[0];
        Assert.Null(prefix.Primitive);
        Assert.Equal(polygonBytes, prefix.PacketLength);
        NativeBattleGfGeometryAttempt mode = result.Batch.Attempts[1];
        Assert.Equal(polygonBytes, mode.ByteOffsetRelative);
        Assert.Equal(0xE1000220u, mode.Primitive!.Words[1]);
        NativeBattleGfGeometryAttempt polygon = result.Batch.Attempts[2];
        Assert.Equal(0, polygon.ByteOffsetRelative);
        Assert.All(polygon.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.NotNull(polygon.Primitive);
        Assert.Equal(mode.Primitive.Bucket, polygon.Primitive!.Bucket);
        if (group is 1 or 5)
        {
            Assert.Equal(4, result.Batch.Attempts.Length);
            Assert.Equal(0xE1000240u, result.Batch.Attempts[3].Primitive!.Words[1]);
            Assert.Equal(polygonBytes + 8, result.Batch.Attempts[3].ByteOffsetRelative);
        }
        else
        {
            Assert.Equal(3, result.Batch.Attempts.Length);
        }
        Assert.Equal(unchecked((uint)-128), U32(result.Context88, 0x18));
        Assert.Equal(SelectedAddress, U32(result.Context88, 4));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(11, 1));
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal(2000, result.Memory.GeometryRegisters.Translation[2]);
    }

    [Fact]
    public void Depth_bias_is_signed_clamped_after_raw_AVSZ_and_never_changes_material_UVs()
    {
        NativeBattleShockwavePulsar260MeshStep first = Draw(3);
        Assert.Equal(500u, first.Memory.GeometryRegisters!.Otz);
        Assert.Equal(372u, U32(first.Context88, 0x2C));
        Assert.Equal(110, first.Batch.Attempts[2].Primitive!.Bucket);
        Assert.Equal(0x12345678u, first.Batch.Attempts[2].Primitive!.Words[3]);
        NativeBattleShockwavePulsar260MeshStep clamped = Draw(3, Context().Write(0x18, BitConverter.GetBytes(-1024)));
        Assert.Equal(0u, U32(clamped.Context88, 0x2C));
        Assert.Equal(17, clamped.Batch.Attempts[2].Primitive!.Bucket);
        Assert.Equal(500u, clamped.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void Near_rejection_keeps_prefix_and_does_not_read_fourth_vertex_cue_or_depth_bias()
    {
        NativeBattleEffectArena context = Context(0x20F0).MarkUnknown(12, 4).MarkUnknown(0x18, 4);
        int reads = 0;
        NativeBattleShockwavePulsar260MeshStep result = Draw(7, context, Memory(0), offset =>
        {
            reads++;
            Assert.True(offset < 24);
            return Vertex(offset);
        });
        Assert.Equal(3, reads);
        Assert.Equal(0, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt prefix = Assert.Single(result.Batch.Attempts);
        Assert.Null(prefix.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, prefix.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, prefix.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, prefix.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x24, 4));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Cue_uses_RGB_three_bytes_and_current_DPCT_register_owner_without_loading_a_matrix()
    {
        NativeBattleShockwavePulsar260MeshStep result = Draw(7, Context(0x20F0));
        Assert.Equal(0x120u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0x340u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0x560u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.NotNull(result.Memory.ScriptedColorRegisters.Material);
        Assert.Throws<NotSupportedException>(() => Draw(7, Context(0x20F0).MarkUnknown(9, 1)));
    }

    [Fact]
    public void Unknown_vertex_W_remains_unknown_without_changing_projection_and_JSON_next()
    {
        NativeBattleShockwavePulsar260MeshStep unknown = Draw(2, readW: _ => null);
        NativeBattleShockwavePulsar260MeshStep known = Draw(2, readW: _ => 1234);
        Assert.Equal(JsonSerializer.Serialize(known.Batch), JsonSerializer.Serialize(unknown.Batch));
        Assert.Null(unknown.Memory.GeometryRegisters!.V1[6]);
        Assert.Null(unknown.Memory.GeometryRegisters.V1[7]);
        NativeBattleShockwavePulsar260MeshStep restored = JsonSerializer.Deserialize<NativeBattleShockwavePulsar260MeshStep>(JsonSerializer.Serialize(unknown))!;
        NativeBattleShockwavePulsar260MeshStep Next(NativeBattleShockwavePulsar260MeshStep value) => Draw(2, value.Context88, value.Memory, readW: _ => null);
        Assert.Equal(JsonSerializer.Serialize(Next(unknown)), JsonSerializer.Serialize(Next(restored)));
    }

    [Fact]
    public void Unreached_groups_and_missing_selected_pointer_fail_without_fabricating_vertex_sources()
    {
        Assert.Throws<NotSupportedException>(() => Draw(4));
        Assert.Throws<NotSupportedException>(() => Draw(3, Context().MarkUnknown(4, 4)));
        Assert.Throws<NotSupportedException>(() => Draw(3, Context(0x32)));
    }

    private const uint Address = 0x112015C;
    private const uint SelectedAddress = 0x20E19B8;

    private static NativeBattleShockwavePulsar260MeshStep Draw(int group, NativeBattleEffectArena? context = null, NativeBattleEffectMemory? memory = null, Func<int, NativeBattleVector>? readVertex = null, Func<int, short?>? readW = null) => NativeBattleShockwavePulsar260Mesh.Draw(memory ?? Memory(2000), Source(group), 0, context ?? Context(), new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, readVertex ?? Vertex, readW ?? (_ => null));

    private static NativeBattleEffectMemory Memory(int z)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }

    private static NativeBattleEffectArena Context(uint flags = 0x2030) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address)).Write(4, BitConverter.GetBytes(SelectedAddress)).Write(8, [0x12, 0x34, 0x56]).Write(12, BitConverter.GetBytes(2048)).Write(0x18, BitConverter.GetBytes(-128)).Write(0x1C, BitConverter.GetBytes(flags));

    private static NativeBattleVector Vertex(int offset) => offset switch { 0 => new(-400, -400, 0), 8 => new(400, -400, 0), 16 => new(-400, 400, 0), 24 => new(400, 400, 0), _ => throw new InvalidDataException("Unexpected selected vertex byte offset.") };
    private static uint U32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));

    private static NativeBattleGfAnimationSource Source(int group)
    {
        int length = group switch { 1 => 12, 2 or 4 => 20, 3 or 5 => 24, 7 => 36, _ => throw new ArgumentOutOfRangeException(nameof(group)) };
        byte[] bytes = new byte[12 + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 12);
        int face = 16 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        uint command = group switch { 1 => 0x28000000, 2 => 0x24000000, 3 => 0x2C000000, 5 => 0x38000000, 7 => 0x3C000000, _ => 0x30000000 };
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), command | 0x112233);
        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        if (group is 2 or 3 or 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x9ABCDEF0);
            if (group != 2)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x76543210);
            }
        }
        if (group is 5 or 7)
        {
            int colors = face + (group == 7 ? 24 : 12);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(colors), 0x30224466);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(colors + 4), 0x306688AA);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(colors + 8), 0x30AACCEE);
        }
        return new(Address, bytes.ToImmutableArray());
    }
}
