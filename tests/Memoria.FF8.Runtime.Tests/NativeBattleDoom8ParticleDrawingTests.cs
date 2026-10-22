using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8ParticleDrawingTests
{
    [Fact]
    public void GT4_F0_keeps_native_packet_layout_context_registers_and_four_depth_payloads()
    {
        NativeBattleStaticMeshResource mesh = Mesh(false);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleCameraProjection projection = new(null, 100, 160, 108);
        NativeBattleEffectArena context = Context(mesh, 2048);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.Draw(Memory(), mesh, matrix, projection, context, Constants);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(52, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
        Assert.Equal(17 + (500 >> 2), primitive.Bucket);
        NativeBattleGfGeometryBatch expected = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, matrix, projection, new(0xF0, 0, 2048, null, null, 0, 17, 2), Constants);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result.Batch));
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, Read(result.Context, 0x20));
        Assert.Equal(result.Memory.GeometryRegisters!.Otz, Read(result.Context, 0x2C));
        Assert.Equal((uint)2048, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(0x3C332211u, result.Memory.ScriptedColorRegisters.Material);
        Assert.All(result.Memory.ScriptedColorRegisters.Rgb, color => Assert.NotNull(color));
        Assert.Equal(new int?[] { 0, 0, 2000 }, result.Memory.GeometryRegisters.Translation);
        Assert.All(result.Memory.GeometryRegisters.Rotation20.Skip(18), value => Assert.Null(value));
        Assert.Equal(0u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Throws<NotSupportedException>(() => result.Context.Read(0x34, 36));
        NativeBattleStaticMeshContext88Step restored = JsonSerializer.Deserialize<NativeBattleStaticMeshContext88Step>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStaticMeshContext88.Draw(result.Memory, mesh, matrix, projection, result.Context, Constants)), JsonSerializer.Serialize(NativeBattleStaticMeshContext88.Draw(restored.Memory, mesh, matrix, projection, restored.Context, Constants)));
    }

    [Fact]
    public void Near_rejected_GT4_keeps_header_and_UVs_but_no_XY_area_depth_or_color_interpolation()
    {
        NativeBattleStaticMeshResource mesh = Mesh(false);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.Draw(Memory(), mesh, NativeBattleCuragaMatrices.Identity8DD770() with { Z = -1 }, new(null, 288, 160, 108), Context(mesh, 2048), Constants);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        foreach (int offset in new[] { 0, 4, 12, 24, 36, 48 })
        {
            Assert.All(attempt.WriteMask.AsSpan().Slice(offset, 4).ToArray(), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        }
        foreach (int offset in new[] { 8, 16, 20, 28, 32, 40, 44 })
        {
            Assert.All(attempt.WriteMask.AsSpan().Slice(offset, 4).ToArray(), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        }
        Assert.Throws<NotSupportedException>(() => result.Context.Read(0x24, 4));
        Assert.Throws<NotSupportedException>(() => result.Context.Read(0x2C, 4));
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Smoke_drawing_uses_common_delivery_and_retained88_after_three_rotations_then_releases_all_temporaries()
    {
        var fixture = Frame(0x8CDFB0);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(fixture.Frame, fixture.Address).WriteDword(0x4C, 0x16350F0).WriteWord(0x50, 2048).WriteWord(0x20, 2000).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
        NativeBattleDoom8Frame frame = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, task);
        bool delivered = false;
        frame = NativeBattleDoom8ParticleDrawing.DrawSmoke(frame, fixture.Address, Resources(), Camera, new(null, 100, 160, 108), Constants, (current, batch, common) =>
        {
            Assert.True(common);
            Assert.Equal(42 * 52, batch.AllocationBytes);
            Assert.Equal(NativeBattleTemporaryMemory.Address + 88, ReadStaging(current, NativeBattleTemporaryMemory.Cursor));
            Assert.Equal(0xF0u, ReadStaging(current, NativeBattleTemporaryMemory.Address + 0x1C));
            Assert.Throws<NotSupportedException>(() => current.Staging!.Read(NativeBattleTemporaryMemory.Address + 0x34, 36));
            delivered = true;
            return current with { State = current.State.WriteDword(0x275F9D4, 123) };
        });
        Assert.True(delivered);
        Assert.Equal(123u, frame.State.ReadDword(0x275F9D4));
        Assert.Equal(NativeBattleTemporaryMemory.Address, ReadStaging(frame, NativeBattleTemporaryMemory.Cursor));
    }

    [Fact]
    public void Dust_frame16_uses_private_D8_delivery_with_unknown_position_W_and_balanced_temporary()
    {
        var fixture = Frame(0x8CD670);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(fixture.Frame, fixture.Address).WriteDword(0x4C, 0x1634F28).WriteWord(0x50, 16).WriteWord(0x20, 2000).MarkPayloadUnknown(0x22, 2);
        NativeBattleDoom8Frame frame = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, task);
        bool delivered = false;
        frame = NativeBattleDoom8ParticleDrawing.DrawDust(frame, fixture.Address, Resources() with { DustSprite = Sprite() }, Camera, new(null, 100, 160, 108), 0, (current, batch, common) =>
        {
            Assert.False(common);
            Assert.Equal(NativeBattleTemporaryMemory.Address + 216, ReadStaging(current, NativeBattleTemporaryMemory.Cursor));
            Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Address + 4, 2)));
            Assert.NotEmpty(batch.Attempts);
            delivered = true;
            return current;
        });
        Assert.True(delivered);
        Assert.Equal(NativeBattleTemporaryMemory.Address, ReadStaging(frame, NativeBattleTemporaryMemory.Cursor));
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Controller.Read(frame, fixture.Address).ReadWord(0x22));
    }

    [Fact]
    public void Hidden_dust_returns_before_resource_or_temporary_reads()
    {
        var fixture = Frame(0x8CD670);
        NativeBattleDoom8Frame frame = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, NativeBattleDoom8Controller.Read(fixture.Frame, fixture.Address).WriteByte(0x26, 4));
        frame = frame with { Staging = null };
        Assert.Same(frame, NativeBattleDoom8ParticleDrawing.DrawDust(frame, fixture.Address, null!, null!, null!, 0, (_, _, _) => throw new InvalidOperationException()));
    }

    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
    private static NativeBattlePackedMatrix32 Camera => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [1, 2, 3] }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static uint Read(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint ReadStaging(NativeBattleDoom8Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(address, 4));

    private static NativeBattleEffectArena Context(NativeBattleStaticMeshResource mesh, uint cue)
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88);
        context = NativeBattleStaticMeshContext88.Put(context, 0, mesh.Address);
        context = NativeBattleStaticMeshContext88.Put(context, 8, 0);
        context = NativeBattleStaticMeshContext88.Put(context, 12, cue);
        return NativeBattleStaticMeshContext88.Put(context, 0x1C, 0xF0);
    }

    private static NativeBattleStaticMeshResource Mesh(bool complete)
    {
        int vertices = complete ? 59 : 4;
        int faces = complete ? 42 : 1;
        int polygon = 8 + vertices * 8;
        byte[] bytes = new byte[polygon + 32 + faces * 36];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), vertices);
        short[] positions = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), positions[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(polygon + 28), faces);
        for (int index = 0; index < faces; index++)
        {
            int face = polygon + 32 + index * 36;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x3C332211);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x00102030);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x00405060);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x708090A0);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x00554433);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 28), 0x00887766);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 32), 0x00BBAA99);
        }
        return new(0, 0x16350F0, bytes.ToImmutableArray());
    }

    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[456];
        bytes[3] = 6;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 17);
        for (int frame = 0; frame < 17; frame++)
        {
            int offset = 48 + frame * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 4;
            bytes[offset + 5] = 128;
            bytes[offset + 6] = 4;
            bytes[offset + 7] = 0x2E;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44), ushort.MaxValue);
        return new(0, 0x1634F28, 0x8CD770, 0x8CD890, bytes.ToImmutableArray());
    }

    private static NativeBattleDoom8Resources Resources() => new(null!, null!, [], [], null!, 0, null!, null!) { SmokeMesh = Mesh(true) };

    private static (NativeBattleDoom8Frame Frame, uint Address) Frame(uint callback)
    {
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        int pool = callback == 0x8CD670 ? 5 : 6;
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, NativeBattleDoom8State.Pools[pool].Stride);
        NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = appended.Hierarchy.Pools };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        return (new NativeBattleDoom8Frame(Memory(), effect, new(123), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray())) { Staging = staging }, appended.Address);
    }
}
