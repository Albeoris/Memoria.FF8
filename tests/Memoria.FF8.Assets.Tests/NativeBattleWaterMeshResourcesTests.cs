using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWaterMeshResourcesTests
{
    [Fact]
    public void Original_selected_shapes_use_custom_face_lists_and_complete_vertex_owners()
    {
        using GameAssets game = Installed();
        ImmutableArray<byte> file0 = game.ReadNativeWater222Resources().Files[0].Data;
        int[] ids = [2, 4, 13];
        int[] offsets = [0x22F8, 0x2600, 0x73B8];
        int[] lengths = [0x218, 0x2A48, 0x5E4];
        int[] vertexCounts = [24, 170, 49];
        short[][] types = [[17], [9], [7, 17]];
        int[][] counts = [[12], [336], [24, 24]];
        for (int index = 0; index < ids.Length; index++)
        {
            NativeBattleWaterMeshShape shape = NativeBattleWaterMeshes.ReadShape(file0, ids[index]);
            Assert.Equal(offsets[index], shape.FileOffset);
            Assert.Equal(lengths[index], shape.Bytes.Length);
            Assert.Equal(vertexCounts[index], shape.VertexCount);
            Assert.Equal(vertexCounts[index] * 8, shape.Vertices.Length);
            Assert.Equal(file0.Skip(offsets[index]).Take(lengths[index]), shape.Bytes);
            Assert.Equal(types[index], shape.Groups().Select(group => group.Type));
            Assert.Equal(counts[index], shape.Groups().Select(group => group.Count));
            NativeBattleWaterMeshShape restored = JsonSerializer.Deserialize<NativeBattleWaterMeshShape>(JsonSerializer.Serialize(shape))!;
            Assert.Equal(JsonSerializer.Serialize(shape.Groups()), JsonSerializer.Serialize(restored.Groups()));
            byte[] projected = new byte[shape.VertexCount * 8];
            for (int vertex = 0; vertex < shape.VertexCount; vertex++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(vertex * 8), (uint)vertex);
                BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(vertex * 8 + 4), 256);
            }
            foreach (NativeBattleWaterMeshGroup group in shape.Groups())
            {
                NativeBattleWaterMeshResult result = NativeBattleWaterMeshPackets.EmitSelected(Frame(), group, projected.ToImmutableArray(), Enumerable.Repeat(256u, shape.VertexCount).ToImmutableArray(), 0x100000, pointer => checked((int)((pointer - 0x1D8E058) / 4)));
                Assert.Equal(group.Count, result.Packets.Attempts.Count(attempt => attempt.Primitive is not null));
                Assert.Equal(group.Count * (group.Type == 7 ? 36 : group.Type == 9 ? 40 : 44), result.Packets.AllocationBytes);
            }
        }
    }

    [Fact]
    public void Current_Arena_shape_reads_only_selected_directory_entry_and_shape_and_observes_actual_mutation()
    {
        using GameAssets game = Installed();
        ImmutableArray<byte> file0 = game.ReadNativeWater222Resources().Files[0].Data;
        NativeBattleWaterMeshShape original = NativeBattleWaterMeshes.ReadShape(file0, 2);
        int directory = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(file0.AsSpan().Slice(12)));
        const int fileOffset = 0x4000;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(fileOffset, file0.Length);
        arena = arena.Write(fileOffset + 12, file0.AsSpan().Slice(12, 4));
        arena = arena.Write(fileOffset + directory, file0.AsSpan().Slice(directory, 4));
        arena = arena.Write(fileOffset + directory + 4 + 2 * 4, file0.AsSpan().Slice(directory + 4 + 2 * 4, 4));
        arena = arena.Write(fileOffset + original.FileOffset, original.Bytes.AsSpan());
        NativeBattleWaterMeshShape current = NativeBattleWaterMeshes.ReadShape(arena, fileOffset, file0.Length, 2);
        Assert.Equal(original.Bytes.ToArray(), current.Bytes.ToArray());
        int vertices = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(original.Bytes.AsSpan().Slice(0x14)));
        byte replacement = unchecked((byte)(original.Bytes[vertices] + 1));
        arena = arena.Write(fileOffset + original.FileOffset + vertices, [replacement]);
        NativeBattleEffectArena cold = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(arena))!;
        NativeBattleWaterMeshShape updated = NativeBattleWaterMeshes.ReadShape(cold, fileOffset, file0.Length, 2);
        Assert.Equal(replacement, updated.Vertices[0]);
        Assert.NotEqual(original.Vertices[0], updated.Vertices[0]);
        Assert.Equal(JsonSerializer.Serialize(original.Groups()), JsonSerializer.Serialize(updated.Groups()));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshes.ReadShape(cold, fileOffset, file0.Length, 4));
    }

    [Fact]
    public void Selected_ingress_rejects_other_shapes_bad_selectors_and_ignores_terminator_type()
    {
        using GameAssets game = Installed();
        ImmutableArray<byte> file0 = game.ReadNativeWater222Resources().Files[0].Data;
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshes.ReadShape(file0, 0));
        NativeBattleWaterMeshShape shape = NativeBattleWaterMeshes.ReadShape(file0, 2);
        int stream = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(shape.Bytes.AsSpan().Slice(8)));
        byte[] broken = shape.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(broken.AsSpan(stream + 4 + 16), 1);
        Assert.Throws<InvalidDataException>(() => (shape with { Bytes = broken.ToImmutableArray() }).Groups());
        BinaryPrimitives.WriteInt16LittleEndian(broken.AsSpan(stream + 2), 0);
        Assert.Throws<InvalidDataException>(() => (shape with { Bytes = broken.ToImmutableArray() }).Groups());
        BinaryPrimitives.WriteInt16LittleEndian(broken.AsSpan(stream), 999);
        BinaryPrimitives.WriteInt16LittleEndian(broken.AsSpan(stream + 2), -1);
        Assert.Empty((shape with { Bytes = broken.ToImmutableArray() }).Groups());
    }

    private static NativeBattleWaterExecutionFrame Frame()
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(1));
        return frame.WriteTemporaryDword(0x50, 0xE1000000).WriteTemporaryDword(0x54, 0).WriteTemporaryDword(0x5C, 0x1D8E058 + 17 * 4).WriteTemporaryDword(0x84, 0x00808080).WriteTemporaryDword(0x8C, 0).WriteTemporaryDword(0x90, 0);
    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources are optional.");
        return new(directory);
    }
}
