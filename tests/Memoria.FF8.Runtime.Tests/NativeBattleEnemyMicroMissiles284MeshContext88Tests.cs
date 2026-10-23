using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnemyMicroMissiles284MeshContext88Tests
{
    [Theory]
    [InlineData(0, 0x20u, 0x2030u)]
    [InlineData(0, 0x20u, 0x20F0u)]
    [InlineData(1, 0x28u, 0x2030u)]
    [InlineData(1, 0x28u, 0x20F0u)]
    [InlineData(1, 0x2Au, 0x2030u)]
    public void Reached_flat_groups_delegate_current_default_ACT_with_literal_source_opcode(int group, uint opcode, uint flags)
    {
        var mesh = Mesh(group, opcode);
        List<int> reads = [];
        var actual = NativeBattleEnemyMicroMissiles284MeshContext88.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(flags), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, offset =>
        {
            reads.Add(offset);
            return (Vertex(offset), null);
        });
        bool quad = group == 1;
        var packet = Assert.Single(actual.Batch.Attempts);
        var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(packet.Primitive);
        Assert.Equal(quad ? new[] { 0, 8, 16, 24 } : new[] { 0, 8, 16 }, reads);
        Assert.Equal(quad ? 24 : 20, actual.Batch.AllocationBytes);
        Assert.Equal(opcode, primitive.Words[1] >> 24);
        Assert.Equal(quad ? 500u : 499u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(quad ? 372u : 371u, U(actual.Context88, 0x2C));
        Assert.Equal(quad ? new[] { 2000, 2000, 2000, 2000 } : new[] { 2000, 2000, 2000, 0 }, primitive.VertexPayload4);
        if (flags == 0x20F0)
        {
            Assert.Equal(opcode << 24 | 0x00112233u, actual.Memory.ScriptedColorRegisters!.Material);
            Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters.Ir0);
        }
        else
        {
            Assert.Null(actual.Memory.ScriptedColorRegisters!.Material);
        }
    }

    [Fact]
    public void Unreached_GT3_group_fails_before_selected_reads()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleEnemyMicroMissiles284MeshContext88.DrawLoaded(Memory(), Mesh(6, 0x36), new(null, 100, 160, 108), Context(0x2030), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, _ => throw new InvalidOperationException()));
    }

    [Fact]
    public void Geometry_larger_than_actual_sixty_four_rows_fails_before_selected_reads()
    {
        var mesh = Mesh(0, 0x20);
        byte[] bytes = mesh.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 65);
        mesh = mesh with { Bytes = bytes.ToImmutableArray() };
        Assert.Throws<InvalidDataException>(() => NativeBattleEnemyMicroMissiles284MeshContext88.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(0x2030), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, _ => throw new InvalidOperationException()));
    }

    private static NativeBattleVector Vertex(int offset) => offset switch { 0 => new(-400, -400, 0), 8 => new(400, -400, 0), 16 => new(0, 400, 0), 24 => new(400, 400, 0), _ => throw new InvalidDataException() };
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(0x10000000u)).Write(4, BitConverter.GetBytes(0x20000000u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048)).Write(24, BitConverter.GetBytes(-128)).Write(28, BitConverter.GetBytes(flags));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleStaticMeshResource Mesh(int group, uint opcode)
    {
        int length = group == 6 ? 28 : 12;
        byte[] bytes = new byte[12 + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        int face = 16 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), opcode << 24 | 0x00112233);
        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
