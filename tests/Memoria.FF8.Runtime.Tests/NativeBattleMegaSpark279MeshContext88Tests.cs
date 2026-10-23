using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMegaSpark279MeshContext88Tests
{
    [Theory]
    [InlineData(2, 0x2030u)]
    [InlineData(3, 0x20F0u)]
    [InlineData(4, 0x20F0u)]
    [InlineData(5, 0x20F0u)]
    [InlineData(6, 0x20F0u)]
    [InlineData(7, 0x20F0u)]
    public void Programme_guard_uses_current_selected_rows_and_retains_depth_bias(int group, uint flags)
    {
        var mesh = Mesh(group);
        var context = Context(mesh.Address, flags).Write(4, BitConverter.GetBytes(0x20000000u)).Write(24, BitConverter.GetBytes(-128));
        List<int> reads = [];
        var actual = NativeBattleMegaSpark279MeshContext88.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, offset =>
        {
            reads.Add(offset);
            return (Vertex(offset), null);
        });
        var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(actual.Batch.Attempts).Primitive);
        bool quad = (group & 1) != 0;
        Assert.Equal(quad ? new[] { 0, 8, 16, 24 } : new[] { 0, 8, 16 }, reads);
        Assert.Equal(quad ? 500u : 499u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(quad ? 372u : 371u, U(actual.Context88, 0x2C));
        Assert.Equal(quad ? new[] { 2000, 2000, 2000, 2000 } : new[] { 2000, 2000, 2000, 0 }, primitive.VertexPayload4);
        Assert.False(actual.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(actual.Context88.ReadSnapshot(11, 1).Known[0]);
        Assert.Equal(unchecked((uint)-128), U(actual.Context88, 24));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint source, uint flags)
    {
        var context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(source)).Write(28, BitConverter.GetBytes(flags));
        if ((flags & 0xC0) != 0)
        {
            context = context.Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048));
        }
        return context;
    }
    private static NativeBattleVector Vertex(int offset) => offset switch { 0 => new(-400, -400, 0), 8 => new(400, -400, 0), 16 => new(0, 400, 0), 24 => new(400, 400, 0), _ => throw new InvalidDataException() };
    private static uint U(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int count = 64;
        int polygon = 12;
        int length = group switch { 2 or 4 => 20, 3 or 5 => 24, 6 => 28, _ => 36 };
        byte[] data = new byte[polygon + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(data, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), count);
        int face = polygon + group * 4 + 4;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(face - 4), 1);
        byte command = group switch { 2 => 0x26, 3 => 0x2E, 4 => 0x32, 5 => 0x3A, 6 => 0x36, _ => 0x3E };
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face), (uint)command << 24 | 0x00112233);
        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        if (group is 2 or 3 or 6 or 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + 12), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + 16), 0x9ABCDEF0);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + 20), 0x12345678);
            }
        }
        if (group is 4 or 5 or 7)
        {
            int start = group == 7 ? 24 : 12;
            for (int corner = 0; corner < (group == 4 ? 2 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + start + corner * 4), 0x00445566u + (uint)corner);
            }
        }
        uint address = 0x10000000;
        return new(0, address, data.ToImmutableArray());
    }
}
