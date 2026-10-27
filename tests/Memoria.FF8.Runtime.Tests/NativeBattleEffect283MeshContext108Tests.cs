using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect283MeshContext108Tests
{
    [Theory]
    [InlineData(1, 0x2030u)]
    [InlineData(1, 0x20F0u)]
    [InlineData(2, 0x2030u)]
    [InlineData(2, 0x20F0u)]
    [InlineData(3, 0x2030u)]
    [InlineData(3, 0x20F0u)]
    [InlineData(4, 0x2030u)]
    [InlineData(4, 0x20F0u)]
    [InlineData(5, 0x2030u)]
    [InlineData(5, 0x20F0u)]
    [InlineData(6, 0x2030u)]
    [InlineData(6, 0x20F0u)]
    [InlineData(7, 0x2030u)]
    [InlineData(7, 0x20F0u)]
    public void Selected_leaves_publish_current_projection_depth_and_color(int group, uint flags)
    {
        List<int> reads = [];
        var mesh = Mesh(group);
        var actual = Draw(Memory(2000), mesh, Context(flags), offset =>
        {
            reads.Add(offset);
            return (Vertex(offset), null);
        });
        bool quad = (group & 1) != 0;
        var packet = Assert.Single(actual.Batch.Attempts);
        var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(packet.Primitive);
        Assert.Equal(quad ? new[] { 0, 8, 16, 24 } : new[] { 0, 8, 16 }, reads);
        Assert.Equal(quad ? 500u : 499u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(quad ? 372u : 371u, U(actual.Context108, 0x40));
        Assert.Equal(110 - (quad ? 0 : 1), primitive.Bucket);
        Assert.Equal(quad ? new[] { 2000, 2000, 2000, 2000 } : new[] { 2000, 2000, 2000, 0 }, primitive.VertexPayload4);
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(actual.Context108, 0x34));
        Assert.Equal(0u, U(actual.Context108, 0x44) & 0x60000);
        Assert.False(actual.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Equal(unchecked((uint)-128), U(actual.Context108, 0x18));
        Assert.Equal(0u, actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        if (flags == 0x20F0)
        {
            Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters.Ir0);
            Assert.NotNull(actual.Memory.ScriptedColorRegisters.Material);
            Assert.NotNull(actual.Memory.ScriptedColorRegisters.Rgb[2]);
        }
        else
        {
            Assert.Null(actual.Memory.ScriptedColorRegisters.Material);
        }
        if (group is 2 or 6)
        {
            int last = group == 2 ? 30 : 38;
            Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[last]);
            Assert.Equal((byte)0, packet.Data[last]);
        }
    }

    [Theory]
    [InlineData(96, true)]
    [InlineData(97, false)]
    [InlineData(144, false)]
    public void Source_geometry_is_bounded_to_96_vertices_before_selected_reads(int count, bool admitted)
    {
        NativeBattleStaticMeshResource original = Mesh(6);
        byte[] bytes = original.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        NativeBattleStaticMeshResource mesh = original with { Bytes = bytes.ToImmutableArray() };
        int reads = 0;
        NativeBattleEffect283MeshContext108Step Run() => Draw(Memory(2000), mesh, Context(0x2030), offset =>
        {
            reads++;
            return (Vertex(offset), (short?)0);
        });
        if (admitted)
        {
            Assert.NotNull(Assert.Single(Run().Batch.Attempts).Primitive);
            Assert.Equal(3, reads);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => Run());
            Assert.Equal(0, reads);
        }
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void Canonical_gouraud_packets_and_current_render_state_survive_JSON(int group)
    {
        NativeBattleEffect283MeshContext108Step actual = Draw(Memory(2000), Mesh(group), Context(0x20F0), offset => (Vertex(offset), (short?)0));
        string json = System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual));
        NativeBattleEffect283MeshContext108Step restored = NativeBattleStorageSnapshots.Restore(System.Text.Json.JsonSerializer.Deserialize<NativeBattleEffect283MeshContext108Step>(json)!);
        Assert.Equal(json, System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal(actual.Memory.GeometryRegisters!.Otz, restored.Memory.GeometryRegisters!.Otz);
        Assert.Equal(actual.Memory.ScriptedColorRegisters!.Ir0, restored.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(U(actual.Context108, 0x40), U(restored.Context108, 0x40));
        NativeBattleGfGeometryAttempt packet = Assert.Single(restored.Batch.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal((byte)(group == 6 ? 0x36 : 0x3E), packet.Data[7]);
        Assert.Equal(actual.Batch.Attempts[0].WriteMask.ToArray(), packet.WriteMask.ToArray());
    }

    private static NativeBattleEffect283MeshContext108Step Draw(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context, Func<int, (NativeBattleVector Vertex, short? W)> read) => NativeBattleEffect283MeshContext108.DrawLoaded(memory, mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, read);
    private static NativeBattleVector Vertex(int offset) => offset switch { 0 => new(-400, -400, 0), 8 => new(400, -400, 0), 16 => new(0, 400, 0), 24 => new(400, 400, 0), _ => throw new InvalidDataException() };
    private static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(0x10000000u)).Write(4, BitConverter.GetBytes(0x20000000u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048)).Write(24, BitConverter.GetBytes(-128)).Write(28, BitConverter.GetBytes(flags)).Write(32, new byte[4]);
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint Command(int group) => (uint)(group switch { 1 => 0x28, 2 => 0x26, 3 => 0x2E, 4 => 0x32, 5 => 0x3A, 6 => 0x36, _ => 0x3E }) << 24 | 0x00112233;
    private static NativeBattleEffectArena Rectangle(NativeBattleEffectArena context, int offset, NativeBattleTextureWindow window) => context.Write(offset, BitConverter.GetBytes(window.X)).Write(offset + 2, BitConverter.GetBytes(window.Y)).Write(offset + 4, BitConverter.GetBytes(window.Width)).Write(offset + 6, BitConverter.GetBytes(window.Height));
    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int length = group switch { 1 => 12, 2 or 4 => 20, 3 or 5 => 24, 6 => 28, _ => 36 };
        byte[] bytes = new byte[12 + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        int face = 16 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), Command(group));
        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        if (group is 2 or 3 or 6 or 7)
        {
            if ((group & 1) == 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0xFA12);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x1234FA12);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x5678FA34);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0xFA78FA56);
            }
        }
        if (group >= 4)
        {
            int start = group == 6 ? 20 : group == 7 ? 24 : 12;
            for (int corner = 1; corner < ((group & 1) != 0 ? 4 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + start + (corner - 1) * 4), 0x00445566u + (uint)corner);
            }
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
