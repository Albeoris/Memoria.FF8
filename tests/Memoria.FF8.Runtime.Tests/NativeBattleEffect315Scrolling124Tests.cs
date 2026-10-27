using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect315Scrolling124Tests
{
    [Theory]
    [InlineData(2, 0x33u)]
    [InlineData(2, 0xF3u)]
    [InlineData(3, 0x33u)]
    [InlineData(3, 0xF3u)]
    [InlineData(7, 0x33u)]
    [InlineData(7, 0xF3u)]
    public void Selected_leaves_publish_inline_windows_shift3_depth_and_current_registers(int group, uint flags)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleEffect315Scrolling124Step result = Draw(mesh, Context(flags));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        bool quad = group != 2;
        uint command = group == 2 ? 0x26u : group == 3 ? 0x2Eu : 0x3Eu;
        Assert.Equal(group == 2 ? 40 : group == 3 ? 48 : 60, result.Batch.AllocationBytes);
        Assert.Equal((uint)(attempt.PacketLength / 4 - 1) << 24, primitive.Words[0]);
        Assert.Equal(NativeBattleEffectTextureWindows.Encode(new(0, 192, 128, 64)), primitive.Words[1]);
        Assert.Equal(0xE2000000u, primitive.Words[^1]);
        Assert.Equal(command << 24 | (flags == 0xF3 ? 0x00081119u : 0x00112233u), primitive.Words[2]);
        Assert.Equal(79, primitive.Bucket);
        Assert.Equal(new[] { 0, 0, 0, 0 }, primitive.VertexPayload4);
        Assert.Equal(quad ? 500u : 499u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(quad ? 500u : 499u, result.Memory.GeometryRegisters.Mac0);
        Assert.Equal(mesh.Address + 8, U(result.Context124, 4));
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(result.Context124, 0x24));
        Assert.Equal(102400u, U(result.Context124, 0x28));
        Assert.Equal(quad ? 500u : 499u, U(result.Context124, 0x30));
        Assert.Equal(0u, U(result.Context124, 0x34) & 0x60000);
        Assert.Equal(0u, U(result.Context124, 0x6C));
        Assert.Equal(0u, U(result.Context124, 0x70));
        Assert.Equal(primitive.Words[1], U(result.Context124, 0x74));
        Assert.Equal(0xE2000000u, U(result.Context124, 0x78));
        Assert.DoesNotContain(true, result.Context124.ReadSnapshot(0x5C, 16).Known);
        Assert.Equal(0u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        if (flags == 0xF3)
        {
            Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
            Assert.Equal(command << 24 | 0x00112233u, result.Memory.ScriptedColorRegisters.Material);
            Assert.Equal(command << 24 | 0x00081119u, result.Memory.ScriptedColorRegisters.Rgb[2]);
            Assert.Equal(new int?[] { 408, 272, 136 }, result.Memory.ScriptedColorRegisters.Mac.ToArray());
            if (group == 7)
            {
                Assert.Equal(new uint?[] { 0x00223344, 0x00334455, 0x3E081119 }, result.Memory.ScriptedColorRegisters.Rgb.ToArray());
            }
        }
        else
        {
            Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Common_axis_wrap_publishes_full_signed_DWORD_scratch_instead_of_truncated_bytes(int group)
    {
        NativeBattleEffect315Scrolling124Step result = Draw(Mesh(group), Context(0x33, 10, 0));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        int count = group == 2 ? 3 : 4;
        int[] expected = [132, -114, -98, -88];
        for (int index = 0; index < count; index++)
        {
            Assert.Equal(unchecked((uint)expected[index]), U(result.Context124, 0x5C + index * 4));
            Assert.Equal(unchecked((byte)expected[index]), attempt.Data[UvOffset(group, index)]);
            Assert.All(result.Context124.ReadSnapshot(0x5C + index * 4, 4).Known, known => Assert.True(known));
        }
        if (group == 2)
        {
            Assert.DoesNotContain(true, result.Context124.ReadSnapshot(0x68, 4).Known);
        }
        Assert.Equal(10u, U(result.Context124, 0x6C));
        Assert.Equal(0u, U(result.Context124, 0x70));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Signed_remainders_keep_native_negative_offsets_and_V_overwrites_shared_scratch_last(int group)
    {
        NativeBattleEffect315Scrolling124Step result = Draw(Mesh(group), Context(0x33, -129, -65));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        int count = group == 2 ? 3 : 4;
        byte[] expectedU = [249, 3, 19, 29];
        byte[] expectedV = [63, 65, 67, 69];
        for (int index = 0; index < count; index++)
        {
            int offset = UvOffset(group, index);
            Assert.Equal(expectedU[index], attempt.Data[offset]);
            Assert.Equal(expectedV[index], attempt.Data[offset + 1]);
            Assert.Equal((uint)expectedV[index], U(result.Context124, 0x5C + index * 4));
        }
        Assert.Equal(uint.MaxValue, U(result.Context124, 0x6C));
        Assert.Equal(uint.MaxValue, U(result.Context124, 0x70));
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result));
        NativeBattleEffect315Scrolling124Step restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect315Scrolling124Step>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Draw(Mesh(group), result.Context124, memory: result.Memory))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Draw(Mesh(group), restored.Context124, memory: restored.Memory))));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Zero_area_rejects_after_UV_scratch_and_partial_packet_stores(int group)
    {
        NativeBattleEffect315Scrolling124Step result = Draw(Mesh(group, zeroArea: true), Context(0xF3, 10, 0));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0u, U(result.Context124, 0x28));
        Assert.DoesNotContain(true, result.Context124.ReadSnapshot(0x30, 4).Known);
        Assert.Equal(132u, U(result.Context124, 0x5C));
        Assert.Equal(unchecked((uint)-114), U(result.Context124, 0x60));
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[16]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[^1]);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Projection_flag_rejection_retains_unreached_context_scratch_and_source_UV_bytes(int group)
    {
        NativeBattleEffect315Scrolling124Step result = Draw(Mesh(group), Context(0xF3, 10, -1), -2000);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.NotEqual(0u, U(result.Context124, 0x34) & 0x60000);
        Assert.DoesNotContain(true, result.Context124.ReadSnapshot(0x28, 4).Known);
        Assert.DoesNotContain(true, result.Context124.ReadSnapshot(0x30, 4).Known);
        Assert.DoesNotContain(true, result.Context124.ReadSnapshot(0x5C, 16).Known);
        Assert.Equal((byte)250, attempt.Data[16]);
        Assert.Equal((byte)0, attempt.Data[17]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[^1]);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result));
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect315Scrolling124Step>(json)!))));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Viewport_rejection_keeps_reached_SXY_and_average_without_publishing_OTZ_context(int group)
    {
        NativeBattleEffect315Scrolling124Step result = Draw(Mesh(group, outside: true), Context(0xF3));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(group == 2 ? 499u : 500u, result.Memory.GeometryRegisters!.Otz);
        Assert.DoesNotContain(true, result.Context124.ReadSnapshot(0x30, 4).Known);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12 + (group == 2 ? 2 : 3) * (group == 7 ? 12 : 8)]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[^1]);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }
    private static NativeBattleEffect315Scrolling124Step Draw(NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context, int z = 2000, NativeBattleEffectMemory? memory = null)
    {
        memory ??= NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return NativeBattleEffect315Scrolling124.DrawLoaded(memory, mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static NativeBattleEffectArena Context(uint flags, int u = 0, int v = 0) => NativeBattleEffectArena.Clear().MarkUnknown(0, 124).Write(0, BitConverter.GetBytes(0x10000000u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(2048u)).Write(0x10, BitConverter.GetBytes(u)).Write(0x14, BitConverter.GetBytes(v)).Write(0x18, BitConverter.GetBytes((short)0)).Write(0x1A, BitConverter.GetBytes((short)192)).Write(0x1C, BitConverter.GetBytes((short)128)).Write(0x1E, BitConverter.GetBytes((short)64)).Write(0x20, BitConverter.GetBytes(flags));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static int UvOffset(int group, int corner) => 16 + corner * (group == 7 ? 12 : 8);

    private static NativeBattleStaticMeshResource Mesh(int group, bool zeroArea = false, bool outside = false)
    {
        int length = group == 2 ? 20 : group == 3 ? 24 : 36;
        byte[] bytes = new byte[8 + 4 * 8 + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        NativeBattleVector[] vertices = zeroArea ? [new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)] : outside ? [new(7000, -400, 0), new(7800, -400, 0), new(7400, 400, 0), new(7800, 400, 0)] : [new(-400, -400, 0), new(400, -400, 0), new(0, 400, 0), new(400, 400, 0)];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            int offset = 8 + vertex * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), vertices[vertex].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), vertices[vertex].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), vertices[vertex].Z);
        }
        int count = 40 + group * 4;
        int face = count + 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(count), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), (group == 2 ? 0x24u : group == 3 ? 0x2Cu : 0x3Cu) << 24 | 0x00112233);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), group == 2 ? (ushort)0x0414 : (ushort)6);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x123400FA);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x56780204);
        if (group != 2)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x061E0414);
        }
        if (group == 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x00224466);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 28), 0x00446688);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 32), 0x006688AA);
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
