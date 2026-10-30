using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSnipeLaser302MeshContext88Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Group19_actual_static_and_selected_source_capture_preserves_full_native_state(bool selected)
    {
        NativeBattleSnipeLaser302MeshContext88Step Run(bool capture)
        {
            var mesh = Mesh(selected ? 3 : 5, !selected);
            var context = Context(mesh.Address, selected ? 0x2030u : 0x30u);
            NativeBattlePilotVisualIdentity? identity = capture ? new(302, 0x25076B8, 1, 123, 4) { ResourceGeneration = mesh.Address } : null;
            if (selected)
            {
                context = context.Write(4, BitConverter.GetBytes(0x20000000u)).Write(24, BitConverter.GetBytes(-128));
                return NativeBattleSnipeLaser302MeshContext88.DrawLoaded(Memory(), mesh, new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, offset => (Vertex(offset), null), identity);
            }
            var result = NativeBattleSnipeLaser302StaticMeshContext88.DrawLoaded(Memory(), mesh, new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, identity);
            return new(result.Memory, result.Context, result.Batch);
        }
        var native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.All(native.Batch.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
        var geometry = Assert.Single(captured.Batch.Attempts).Primitive!.PilotGeometry!;
        Assert.NotNull(geometry);
        Assert.Equal((ushort)302, geometry.Identity.Effect);
        Assert.Equal(123, geometry.Identity.Generation);
        Assert.Equal(4, geometry.CornerCount);
    }

    [Theory]
    [InlineData(3, 0x30u)]
    [InlineData(3, 0xF0u)]
    [InlineData(5, 0x30u)]
    [InlineData(5, 0xF0u)]
    [InlineData(7, 0x30u)]
    [InlineData(7, 0xF0u)]
    public void Static_guard_uses_current_inline_source_W119_and_native_UV_reset(int group, uint flags)
    {
        var mesh = Mesh(group, true);
        var context = Context(mesh.Address, flags);
        var actual = NativeBattleSnipeLaser302StaticMeshContext88.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2);
        var packet = Assert.Single(actual.Batch.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal(0u, U(actual.Context, 24));
        Assert.Equal(mesh.Address + 8, U(actual.Context, 4));
        Assert.Equal((ushort)119, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.True(actual.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.Equal(500u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(500u, U(actual.Context, 0x2C));
        Assert.False(actual.Context.ReadSnapshot(11, 1).Known[0]);
        if (flags == 0x30)
        {
            Assert.Null(actual.Memory.ScriptedColorRegisters.Material);
        }
        else
        {
            Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters.Ir0);
            Assert.NotNull(actual.Memory.ScriptedColorRegisters.Material);
        }
    }

    [Fact]
    public void Programme_guard_delegates_selected_FT3_with_current_unknown_W()
    {
        var mesh = Mesh(2, false);
        var context = Context(mesh.Address, 0x2030).Write(4, BitConverter.GetBytes(0x20000000u)).Write(24, BitConverter.GetBytes(-128));
        List<int> reads = [];
        var actual = NativeBattleSnipeLaser302MeshContext88.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, offset =>
        {
            reads.Add(offset);
            return (Vertex(offset), null);
        });
        var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(new[] { 0, 8, 16 }, reads);
        Assert.Equal(499u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(371u, U(actual.Context88, 0x2C));
        Assert.Equal(new[] { 2000, 2000, 2000, 0 }, primitive.VertexPayload4);
        Assert.False(actual.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
    }

    [Fact]
    public void Programme_guard_rejects_unreached_GT3_before_selected_reads()
    {
        var mesh = Mesh(6, false);
        Assert.Throws<NotSupportedException>(() => NativeBattleSnipeLaser302MeshContext88.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(mesh.Address, 0x2030), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, _ => throw new InvalidOperationException()));
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
    private static NativeBattleStaticMeshResource Mesh(int group, bool inline)
    {
        int count = inline ? 220 : 4;
        int polygon = inline ? 8 + count * 8 : 12;
        int length = group switch { 2 => 20, 3 or 5 => 24, 6 => 28, _ => 36 };
        byte[] data = new byte[polygon + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(data, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), count);
        if (inline)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                var vertex = Vertex(corner * 8);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8 + corner * 8), vertex.X);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10 + corner * 8), vertex.Y);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12 + corner * 8), vertex.Z);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + corner * 8), 119);
            }
        }
        int face = polygon + group * 4 + 4;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(face - 4), 1);
        byte command = group switch { 2 => 0x26, 3 => 0x2E, 5 => 0x3A, 6 => 0x36, _ => 0x3E };
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
        if (group is 5 or 7)
        {
            int start = group == 5 ? 12 : 24;
            for (int corner = 0; corner < 3; corner++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + start + corner * 4), 0x00445566u + (uint)corner);
            }
        }
        return new(0, inline ? 0x103462Cu : 0x10000000u, data.ToImmutableArray());
    }
}
