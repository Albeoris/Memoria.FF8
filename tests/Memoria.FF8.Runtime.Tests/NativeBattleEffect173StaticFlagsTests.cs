using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect173StaticFlagsTests
{
    [Theory]
    [InlineData(4, 0x2030u)]
    [InlineData(4, 0x20F0u)]
    [InlineData(5, 0x2030u)]
    [InlineData(5, 0x20F0u)]
    public void Selected_programme_flags_preserve_full_header_and_use_actual_selected_vertices(int group, uint flags)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(4, BitConverter.GetBytes(mesh.Address + 12)).Write(12, BitConverter.GetBytes(819)).Write(0x1C, BitConverter.GetBytes(flags));
        if ((flags & 0xC0) != 0)
        {
            context = context.Write(8, new byte[3]);
        }
        List<int> selected = [];
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.DrawLoaded(NativeBattleEffect173Mesh172Tests.Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, mesh.Address + 12, ReadSelected);
        uint low = flags & 0xFF;
        NativeBattleEffectArena ordinaryContext = context.Write(0x1C, BitConverter.GetBytes(low));
        NativeBattleStaticMeshContext88Step ordinary = NativeBattleStaticMeshContext88.DrawLoaded(NativeBattleEffect173Mesh172Tests.Memory(), mesh, new(null, 100, 160, 108), ordinaryContext, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, readInlineVertex: Vertex);
        Assert.NotEmpty(selected);
        Assert.Equal(flags, BinaryPrimitives.ReadUInt32LittleEndian(result.Context.Read(0x1C, 4)));
        Assert.Equal(mesh.Address + 12, BinaryPrimitives.ReadUInt32LittleEndian(result.Context.Read(4, 4)));
        Assert.Equal(ordinary.Batch.AllocationBytes, result.Batch.AllocationBytes);
        Assert.Equal(Assert.Single(ordinary.Batch.Attempts).Primitive!.Words, Assert.Single(result.Batch.Attempts).Primitive!.Words);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(ordinary.Memory.ScriptedColorRegisters), System.Text.Json.JsonSerializer.Serialize(result.Memory.ScriptedColorRegisters));
        if (low == 0x30)
        {
            Assert.DoesNotContain(true, result.Context.ReadSnapshot(8, 3).Known);
        }

        (NativeBattleVector Vertex, short? W) ReadSelected(int offset)
        {
            selected.Add(offset);
            return Vertex(offset);
        }
        (NativeBattleVector Vertex, short? W) Vertex(int offset)
        {
            ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan().Slice(12 + offset, 8);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), 0);
        }
    }

    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        const int polygon = 44;
        int length = group == 4 ? 20 : 24;
        byte[] bytes = new byte[polygon + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        (short X, short Y)[] positions = [(-100, -100), (100, -100), (-100, 100), (100, 100)];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), positions[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), positions[index].Y);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(polygon + group * 4), 1);
        int face = polygon + (group + 1) * 4;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 4 ? 0x30808080u : 0x38808080u);
        for (int index = 0; index < (group == 4 ? 3 : 4); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + index * 2), (ushort)(index * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x00402010);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x00804020);
        if (group == 5)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x00C08040);
        }
        return new(0, 0x200000, bytes.ToImmutableArray());
    }
}
