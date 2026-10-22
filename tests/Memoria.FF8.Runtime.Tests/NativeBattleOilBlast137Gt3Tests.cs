using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilBlast137Gt3Tests
{
    [Theory]
    [InlineData(0x30u, 2000, false)]
    [InlineData(0xC3u, 2000, false)]
    [InlineData(0xC3u, 2000, true)]
    [InlineData(0x30u, 1, false)]
    public void Selected_GT3_reuses_the_proved_572200_packet_and_register_contract(uint flags, int z, bool reverse)
    {
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = z };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with
        {
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(),
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters!, matrix, true, true, null),
            PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) }
        };
        NativeBattleStaticMeshResource mesh = Mesh(reverse);
        NativeBattleCameraProjection projection = new(null, 100, 160, 108);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[] { 4, 8, 12 }).Write(12, BitConverter.GetBytes(2048)).Write(0x1C, BitConverter.GetBytes(flags));
        NativeBattleStaticMeshContext88Step expected = NativeBattleStaticMeshContext88.DrawLoaded(memory, mesh, projection, context, colors);
        NativeBattleEffectArena selectedContext = context.Write(4, BitConverter.GetBytes(mesh.Address + 8)).Write(0x1C, BitConverter.GetBytes(flags | 0x2000));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics!;
        NativeBattleCuraga88MeshStep actual = NativeBattleCuraga88Mesh.EmitBatch(mesh.Bytes, mesh.Address, matrix, projection, selectedContext, new(memory.GeometryRegisters!, memory.ScriptedColorRegisters!), memory.PrimitiveProcess!.ModelRendererRegisters!, controls.Dqa, controls.Dqb, controls.Zsf4, colors, zsf3: controls.Zsf3, selectedVerticesAddress: mesh.Address + 8, readSelectedVertex: offset => ReadVertex(mesh.Bytes, offset));
        Assert.Equal(JsonSerializer.Serialize(expected.Batch), JsonSerializer.Serialize(actual.Batch));
        Assert.Equal(JsonSerializer.Serialize(expected.Memory.GeometryRegisters), JsonSerializer.Serialize(actual.Registers.Geometry));
        Assert.Equal(JsonSerializer.Serialize(expected.Memory.ScriptedColorRegisters), JsonSerializer.Serialize(actual.Registers.Colors));
        Assert.Equal(expected.Memory.PrimitiveProcess!.ModelRendererRegisters, actual.FarControls);
        Assert.Throws<NotSupportedException>(() => actual.Context88.Read(11, 1));
        if (z == 2000 && !reverse)
        {
            Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
            Assert.Equal(40, actual.Batch.AllocationBytes);
        }
        else
        {
            Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        }
    }

    [Fact]
    public void Selected_GT3_preserves_unknown_vertex_W_without_consuming_it()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleStaticMeshResource mesh = Mesh(false);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(4, BitConverter.GetBytes(0x2100000u)).Write(0x1C, BitConverter.GetBytes(0x2030u));
        NativeBattleCuraga88MeshStep step = NativeBattleCuraga88Mesh.EmitBatch(mesh.Bytes, mesh.Address, matrix, new(null, 100, 160, 108), context, new(memory.GeometryRegisters!, memory.ScriptedColorRegisters!), memory.PrimitiveProcess!.ModelRendererRegisters!, 0, 0, 1024, NativeBattleGteColorInterpolation.CodeConstants45F270, zsf3: 1365, selectedVerticesAddress: 0x2100000, readSelectedVertex: offset => (ReadVertex(mesh.Bytes, offset).Vertex, null));
        Assert.NotNull(Assert.Single(step.Batch.Attempts).Primitive);
        Assert.Null(step.Registers.Geometry.V1[6]);
        Assert.Null(step.Registers.Geometry.V2[6]);
        Assert.Null(step.FarControls.FarRed);
    }

    private static NativeBattleStaticMeshResource Mesh(bool reverse)
    {
        byte[] bytes = new byte[92];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        NativeBattleVector[] vertices = [new(0, 0, 0), new(200, 0, 0), new(0, 200, 0)];
        for (int index = 0; index < 3; index++)
        {
            int offset = 8 + index * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 6), (short)(0x1230 + index));
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(56), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x34010203);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(64), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(66), reverse ? (ushort)4 : (ushort)2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(68), reverse ? (ushort)2 : (ushort)4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x11223344);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), 0x55667788);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), 0x00112233);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84), 0x00445566);
        return new(0, 0x1100000, bytes.ToImmutableArray());
    }

    private static (NativeBattleVector Vertex, short? W) ReadVertex(ImmutableArray<byte> bytes, int offset)
    {
        ReadOnlySpan<byte> packed = bytes.AsSpan().Slice(8 + offset, 8);
        return (new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed[2..]), BinaryPrimitives.ReadInt16LittleEndian(packed[4..])), BinaryPrimitives.ReadInt16LittleEndian(packed[6..]));
    }
}
