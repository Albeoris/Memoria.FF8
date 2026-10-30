using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShotgun281MeshContext88Tests
{
    [Theory]
    [InlineData(4, 0x2030u)]
    [InlineData(4, 0x20F0u)]
    [InlineData(6, 0x2030u)]
    [InlineData(6, 0x20F0u)]
    public void Actual_triangles_use_three_reads_AVSZ3_and_current_DPCT_fifo(int group, uint flags)
    {
        List<int> reads = [];
        var mesh = Mesh(group);
        var actual = NativeBattleShotgun281MeshContext88.DrawLoaded(Memory(2000), mesh, new(null, 100, 160, 108), Context(flags), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, offset =>
        {
            reads.Add(offset);
            return (Vertex(offset), null);
        });
        var packet = Assert.Single(actual.Batch.Attempts);
        var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(packet.Primitive);
        Assert.Equal(new[] { 0, 8, 16 }, reads);
        Assert.Equal(group == 4 ? 28 : 40, actual.Batch.AllocationBytes);
        Assert.Equal(499u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(371u, U(actual.Context88, 0x2C));
        Assert.Equal(109, primitive.Bucket);
        Assert.Equal(new[] { 2000, 2000, 2000, 0 }, primitive.VertexPayload4);
        Assert.Equal(unchecked((uint)-128), U(actual.Context88, 0x18));
        Assert.False(actual.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        if (group == 6)
        {
            Assert.Equal(0x12345678u, primitive.Words[3]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[39]);
            Assert.Equal((byte)0, packet.Data[38]);
            Assert.Equal((byte)0, packet.Data[39]);
        }
        if (flags == 0x20F0)
        {
            uint command = group == 4 ? 0x32112233u : 0x36112233u;
            Assert.Equal(command, actual.Memory.ScriptedColorRegisters.Material);
            Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters.Ir0);
            Assert.Equal(new uint?[] { (command & 0xFF000000) | 0x00222A33, (command & 0xFF000000) | 0x003B444C, (command & 0xFF000000) | 0x00081119 }, actual.Memory.ScriptedColorRegisters.Rgb);
        }
        else
        {
            Assert.Equal(0u, actual.Memory.ScriptedColorRegisters.Ir0);
            Assert.Null(actual.Memory.ScriptedColorRegisters.Material);
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void Triangle_near_prefix_does_not_consume_bias_or_cue(int group)
    {
        var actual = NativeBattleShotgun281MeshContext88.DrawLoaded(Memory(1), Mesh(group), new(null, 100, 160, 108), Context(0x20F0).MarkUnknown(8, 8).MarkUnknown(24, 4), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, offset => (Vertex(offset), null));
        var packet = Assert.Single(actual.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => actual.Context88.Read(0x2C, 4));
        Assert.Null(actual.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void Wrapper_rejects_an_unreached_group_before_selected_reads()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleShotgun281MeshContext88.DrawLoaded(Memory(2000), Mesh(2), new(null, 100, 160, 108), Context(0x2030), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, _ => throw new InvalidOperationException()));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Presentation_preserves_every_reached_group_native_context_FIFO_packet_and_selected_reads(int group)
    {
        NativeBattleStaticMeshResource mesh = PresentationMesh(group);
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattlePilotVisualIdentity identity = new(281, 0x1234, 2, 3, 4) { ResourceGeneration = 5 };
        List<int> nativeReads = [];
        List<int> capturedReads = [];
        NativeBattleVector Read(int offset) => offset == 24 ? new(400, 400, 0) : Vertex(offset);
        var native = NativeBattleShotgun281MeshContext88.DrawLoaded(Memory(2000), mesh, projection, Context(0x20F0), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, offset =>
        {
            nativeReads.Add(offset);
            return (Read(offset), null);
        });
        string nativeMemory = NativeBattleTestSnapshots.Json(native.Memory);
        string nativeContext = NativeBattleTestSnapshots.Json(native.Context88);
        string nativeBatch = NativeBattleTestSnapshots.Json(native.Batch);
        var captured = NativeBattleShotgun281MeshContext88.DrawLoaded(Memory(2000), mesh, projection, Context(0x20F0), NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, offset =>
        {
            capturedReads.Add(offset);
            return (Read(offset), null);
        }, pilotIdentity: identity);
        Assert.Equal(nativeMemory, NativeBattleTestSnapshots.Json(captured.Memory));
        Assert.Equal(nativeContext, NativeBattleTestSnapshots.Json(captured.Context88));
        Assert.Equal(nativeBatch, NativeBattleTestSnapshots.Json(captured.Batch));
        Assert.Equal(nativeReads, capturedReads);
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Batch.Attempts, attempt => attempt.Primitive is not null).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal((ushort)281, geometry.Identity.Effect);
        Assert.Equal(identity.Generation, geometry.Identity.Generation);
        Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
        Assert.Equal(new System.Numerics.Vector3(-400, -400, 2000), geometry.A);
        Assert.Equal((byte)((group & 1) == 0 ? 3 : 4), geometry.CornerCount);
        Assert.Equal(nativeReads.Count, geometry.CornerCount);
        Assert.Equal(nativeBatch, NativeBattleTestSnapshots.Json(captured.Batch));
    }

    private static NativeBattleStaticMeshResource PresentationMesh(int group)
    {
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] bytes = new byte[12 + 32 + lengths[group]];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        int face = 16 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        byte[] opcodes = [0x20, 0x28, 0x24, 0x2C, 0x30, 0x38, 0x34, 0x3C];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), (uint)opcodes[group] << 24 | 0x808080);
        for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), checked((ushort)(corner * 2)));
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }

    private static NativeBattleVector Vertex(int offset) => offset switch { 0 => new(-400, -400, 0), 8 => new(400, -400, 0), 16 => new(0, 400, 0), _ => throw new InvalidDataException() };
    private static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(0x10000000u)).Write(4, BitConverter.GetBytes(0x20000000u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(2048)).Write(24, BitConverter.GetBytes(-128)).Write(28, BitConverter.GetBytes(flags));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int length = group == 6 ? 28 : 20;
        byte[] bytes = new byte[12 + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        int face = 16 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 4 ? 0x32112233u : 0x36112233u);
        for (int corner = 0; corner < 3; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        if (group == 6)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0xABCD);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x9ABCDEF0);
        }
        int color = group == 6 ? 20 : 12;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + color), 0x00445566);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + color + 4), 0x00778899);
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
