using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect283MeshInterleavingTests
{
    [Fact]
    public void Immediate_packet_stores_change_overlapping_selected_rows_before_the_next_face()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int vertex = 0; vertex < 3; vertex++)
        {
            NativeBattleVector point = Vertex(vertex * 8);
            arena = arena.Write(0x648 + vertex * 8, BitConverter.GetBytes(point.X)).Write(0x64A + vertex * 8, BitConverter.GetBytes(point.Y)).Write(0x64C + vertex * 8, BitConverter.GetBytes(point.Z)).Write(0x64E + vertex * 8, new byte[2]);
        }
        List<(int Offset, NativeBattleVector Vertex, int Delivered)> reads = [];
        List<(NativeBattleGfGeometryAttempt Attempt, int Allocation)> delivered = [];
        int allocated = 0;
        NativeBattleEffect283MeshContext108Step result = Draw(offset =>
        {
            byte[] bytes = arena.Read(0x648 + offset, 8);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
            reads.Add((offset, point, delivered.Count));
            return (point, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)));
        }, (attempt, allocation) =>
        {
            Assert.Equal(allocated, attempt.ByteOffsetRelative);
            delivered.Add((attempt, allocation));
            for (int index = 0; index < attempt.PacketLength; index++)
            {
                int destination = 0x650 + attempt.ByteOffsetRelative + index;
                arena = attempt.WriteMask[index] switch
                {
                    NativeBattleGfPacketByteState.Known => arena.Write(destination, [attempt.Data[index]]),
                    NativeBattleGfPacketByteState.Unknown => arena.MarkUnknown(destination, 1),
                    NativeBattleGfPacketByteState.Untouched => arena,
                    _ => throw new InvalidDataException()
                };
            }
            allocated += allocation;
        });
        Assert.Equal(6, reads.Count);
        Assert.All(reads.Take(3), read => Assert.Equal(0, read.Delivered));
        Assert.All(reads.Skip(3), read => Assert.Equal(1, read.Delivered));
        Assert.Equal(Vertex(8), reads[1].Vertex);
        Assert.NotEqual(reads[1].Vertex, reads[4].Vertex);
        Assert.Equal(2, delivered.Count);
        Assert.NotNull(delivered[0].Attempt.Primitive);
        Assert.Equal(delivered[0].Allocation, delivered[1].Attempt.ByteOffsetRelative);
        Assert.Equal(allocated, result.Batch.AllocationBytes);
        Assert.Equal(result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative), delivered.Select(value => value.Attempt.ByteOffsetRelative));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Observer_preserves_shader_state_context_masks_and_zero_allocation_attempts(bool reject)
    {
        NativeBattleEffect283MeshContext108Step ordinary = Draw(offset => (Vertex(offset), (short?)0), null, reject);
        List<(int Offset, int Allocation)> writes = [];
        NativeBattleEffect283MeshContext108Step observed = Draw(offset => (Vertex(offset), (short?)0), (attempt, allocation) => writes.Add((attempt.ByteOffsetRelative, allocation)), reject);
        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(observed));
        Assert.Equal(2, writes.Count);
        Assert.Equal(0, writes[0].Offset);
        Assert.Equal(writes[0].Allocation, writes[1].Offset);
        Assert.Equal(observed.Batch.AllocationBytes, writes.Sum(value => value.Allocation));
        if (reject)
        {
            Assert.All(writes, value => Assert.Equal(0, value.Allocation));
            Assert.All(observed.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        }
        else
        {
            Assert.All(writes, value => Assert.True(value.Allocation > 0));
        }
    }

    private static NativeBattleEffect283MeshContext108Step Draw(Func<int, (NativeBattleVector Vertex, short? W)> read, Action<NativeBattleGfGeometryAttempt, int>? observe, bool reject = false)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = reject ? -2000 : 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(0x10000000u)).Write(4, BitConverter.GetBytes(0x20E0100u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048)).Write(24, BitConverter.GetBytes(-128)).Write(28, BitConverter.GetBytes(0x20F0u)).Write(32, new byte[4]);
        return NativeBattleEffect283MeshContext108.DrawLoaded(memory, Mesh(), new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, read, observe);
    }

    private static NativeBattleVector Vertex(int offset) => offset switch
    {
        0 => new(-400, -400, 0),
        8 => new(400, -400, 0),
        16 => new(0, 400, 0),
        _ => throw new InvalidDataException()
    };

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[12 + 32 + 56];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        int count = 12 + 6 * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(count), 2);
        for (int index = 0; index < 2; index++)
        {
            int face = count + 4 + index * 28;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x36112233);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0xFA12);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x1234FA12);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x5678FA34);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x00445566);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x00778899);
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
