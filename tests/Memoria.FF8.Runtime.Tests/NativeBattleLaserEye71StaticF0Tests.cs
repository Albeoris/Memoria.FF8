using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLaserEye71StaticF0Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void F0_flat_triangle_shades_RGB_and_retains_full_IR0_and_native_FIFO(int group)
    {
        NativeBattleStaticMeshContext88Step result = Draw(group, Context(0x12340800));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        uint command = group == 0 ? 0x20000000u : 0x24000000u;
        Assert.Equal(group == 0 ? 20 : 32, attempt.PacketLength);
        Assert.Equal(attempt.PacketLength, result.Batch.AllocationBytes);
        Assert.Equal(command | 0x081119u, attempt.Primitive!.Words[1]);
        Assert.Equal(command | 0x112233u, result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(0x12340800u, result.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(new uint?[] { 0x22112233, 0x33445566, command | 0x081119u }, result.Memory.ScriptedColorRegisters.Rgb.ToArray());
        Assert.Equal(new int?[] { 408, 272, 136 }, result.Memory.ScriptedColorRegisters.Mac.ToArray());
        Assert.Equal(new int?[] { 408, 272, 136 }, result.Memory.ScriptedColorRegisters.Ir.ToArray());
        Assert.Equal(0u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.Equal(499u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(141, attempt.Primitive.Bucket);
        Assert.Throws<NotSupportedException>(() => result.Context.Read(11, 1));
        if (group == 2)
        {
            Assert.Equal(0x12345678u, attempt.Primitive.Words[3]);
            Assert.Equal(0x9ABCDEF0u, attempt.Primitive.Words[5]);
            Assert.Equal(0x7654u, attempt.Primitive.Words[7]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void F0_negative_cue_uses_signed_low_WORD_and_bit10_bypasses_backface(int group)
    {
        NativeBattleStaticMeshContext88Step result = Draw(group, Context(0x1357F800), reverse: true);
        NativeBattleGeometryPrimitive packet = Assert.Single(result.Batch.Attempts).Primitive!;
        Assert.Equal((group == 0 ? 0x20000000u : 0x24000000u) | 0x19334Cu, packet.Words[1]);
        Assert.True(unchecked((int)U32(result.Context, 0x24)) < 0);
        Assert.Equal(0x1357F800u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 1224, 816, 408 }, result.Memory.ScriptedColorRegisters.Mac.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void F0_near_rejection_preserves_packet_prefix_without_reading_unknown_cue(int group)
    {
        NativeBattleStaticMeshContext88Step result = Draw(group, Context(0).MarkUnknown(12, 4), z: 0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        if (group == 2)
        {
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[20]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[28]);
        }
        NativeBattleStaticMeshContext88Step baseline = Draw(group, Context(0).MarkUnknown(12, 4).Write(0x1C, BitConverter.GetBytes(0u)), z: 0);
        Assert.Equal(baseline.Memory.ScriptedColorRegisters!.Material, result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(baseline.Memory.ScriptedColorRegisters.Ir0, result.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(0u, result.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(new uint?[] { 0x11010203, 0x22112233, 0x33445566 }, result.Memory.ScriptedColorRegisters.Rgb.ToArray());
        Assert.Throws<NotSupportedException>(() => result.Context.Read(0x24, 4));
        Assert.Throws<NotSupportedException>(() => result.Context.Read(0x2C, 4));
    }

    [Fact]
    public void F0_JSON_next_preserves_feedback_and_unknown_context_RGB_high_byte()
    {
        NativeBattleStaticMeshContext88Step first = Draw(2, Context(2048));
        NativeBattleStaticMeshContext88Step restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleStaticMeshContext88Step>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        NativeBattleStaticMeshContext88Step Next(NativeBattleStaticMeshContext88Step current) => Draw(0, current.Context, current.Memory);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(first))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(restored))));
        Assert.Throws<NotSupportedException>(() => Draw(0, Context(2048).MarkUnknown(9, 1)));
    }

    [Fact]
    public void F0_flat_quad_shades_RGB_and_retains_fourth_vertex_feedback()
    {
        NativeBattleStaticMeshContext88Step result = Draw(1, Context(2048));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal(24, result.Batch.AllocationBytes);
        Assert.Equal(0x28081119u, attempt.Primitive!.Words[1]);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new uint?[] { 0x22112233, 0x33445566, 0x28081119 }, result.Memory.ScriptedColorRegisters.Rgb.ToArray());
        Assert.Equal(500u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(500u, U32(result.Context, 0x2C));
        Assert.Equal((short)44, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan(6, 2)));
        Assert.Throws<NotSupportedException>(() => Draw(1, Context(2048).MarkUnknown(9, 1)));
    }

    private const uint Address = 0x158F688;
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Context(uint cue) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address)).Write(8, [0, 0, 0]).Write(12, BitConverter.GetBytes(cue)).Write(0x1C, BitConverter.GetBytes(0xF0u));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [0x11010203, 0x22112233, 0x33445566] }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleStaticMeshContext88Step Draw(int group, NativeBattleEffectArena context, NativeBattleEffectMemory? memory = null, int z = 2000, bool reverse = false) => NativeBattleStaticMeshContext88.Draw(memory ?? Memory(), Mesh(group, reverse), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    private static NativeBattleStaticMeshResource Mesh(int group, bool reverse)
    {
        int stride = group == 2 ? 20 : 12;
        int vertexCount = group == 1 ? 4 : 3;
        int header = 8 + vertexCount * 8;
        byte[] bytes = new byte[header + 32 + stride];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, header);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), vertexCount);
        short[] positions = group == 1 ? [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44] : [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), positions[index]);
        }
        int face = header + 4 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), (group == 1 ? 0x28000000u : group == 2 ? 0x24000000u : 0x20000000u) | 0x112233u);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), reverse ? (ushort)4 : (ushort)2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), reverse ? (ushort)2 : (ushort)4);
        if (group == 1)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 6);
        }
        if (group == 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0x7654);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x9ABCDEF0);
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
