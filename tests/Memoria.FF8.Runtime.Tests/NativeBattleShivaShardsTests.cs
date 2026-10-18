using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaShardsTests
{
    [Fact]
    public void Source_triangles_generate_signed_centroids_one_sample_each_and_group_specific_rotation_signs()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture();
        NativeBattleShivaEarlyTaskFrame result = NativeBattleShivaShards.Initialize5C1EE0(frame, Mesh());
        NativeBattleEffectArena arena = result.Memory.Arena!;
        Assert.Equal(new NativeCrtRandomState(0x5F1E8D4B), result.Random);
        for (int index = 0; index < 170; index++)
        {
            int address = 0x2E000 + index * 32;
            Assert.Equal(unchecked((ushort)-1), Word(arena, address));
            Assert.Equal((ushort)3, Word(arena, address + 2));
            Assert.Equal((ushort)1000, Word(arena, address + 4));
            Assert.Equal((ushort)3, Word(arena, address + 6));
            Assert.Equal((ushort)0, Word(arena, address + 0x0A));
            Assert.Equal((ushort)0, Word(arena, address + 0x0E));
            Assert.Equal((ushort)0, Word(arena, address + 0x18));
            Assert.Equal((ushort)0, Word(arena, address + 0x1C));
        }
        Assert.Equal(unchecked((ushort)-143), Word(arena, 0x2E012));
        Assert.Equal((ushort)16, Word(arena, 0x2E01A));
        Assert.Equal((ushort)40, Word(arena, 0x2E01E));
        int firstGouraud = 0x2E000 + 102 * 32;
        Assert.Equal(unchecked((ushort)-137), Word(arena, firstGouraud + 0x12));
        Assert.Equal(unchecked((ushort)-112), Word(arena, firstGouraud + 0x1A));
        Assert.Equal((ushort)24, Word(arena, firstGouraud + 0x1E));
        Assert.Equal((ushort)32, Word(arena, firstGouraud + 32 + 0x1A));
        Assert.Equal(unchecked((ushort)-24), Word(arena, firstGouraud + 32 + 0x1E));
        Assert.Equal(new byte[] { 0 }, arena.Read(0x2DFFF, 1));
        Assert.Equal(new byte[] { 0 }, arena.Read(0x2F540, 1));
    }

    [Fact]
    public void Spawn_sets_only_explicit_task_fields_and_sparse_geometry_preserves_unknown_record_suffixes()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(0x2E000, 170 * 32) } };
        NativeBattleShivaEarlyTaskFrame result = NativeBattleShivaShards.Initialize5C1E80(frame, Mesh());
        NativeBattleModulePoolTask child = result.Pool.Slots[0];
        Assert.Equal(0x5C2260u, child.Callback);
        Assert.Equal((ushort)0, child.ReadWord(0x0C));
        Assert.Equal((ushort)0, child.ReadWord(0x10));
        Assert.Equal((ushort)0, child.ReadWord(0x12));
        Assert.Equal((ushort)0, child.ReadWord(0x14));
        Assert.Equal((ushort)0x1400, child.ReadWord(0x1C));
        Assert.Equal((ushort)0x2200, child.ReadWord(0x20));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(0x0E));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(0x18));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(0x1A));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(0x1E));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(0x22));
        foreach (int offset in new[] { 8, 12, 16, 20, 22 })
        {
            Assert.Throws<NotSupportedException>(() => result.Memory.Arena!.Read(0x2E000 + offset, 2));
            Assert.Throws<NotSupportedException>(() => result.Memory.Arena!.Read(0x2E000 + 169 * 32 + offset, 2));
        }
        Assert.Equal((ushort)3, Word(result.Memory.Arena!, 0x2E006));
        NativeBattleShivaEarlyTaskFrame restored = JsonSerializer.Deserialize<NativeBattleShivaEarlyTaskFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleShivaEarlyTaskFrame independent = NativeBattleShivaShards.Initialize5C1E80(restored, Mesh());
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(independent));
    }

    [Fact]
    public void Unexpected_group_vertex_and_signed_radius_inputs_fail_without_inventing_geometry()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture();
        NativeBattleStaticMeshResource mesh = Mesh();
        byte[] badGroup = mesh.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(badGroup.AsSpan(0x348), 103);
        Assert.Throws<InvalidDataException>(() => NativeBattleShivaShards.Initialize5C1EE0(frame, mesh with { Bytes = badGroup.ToImmutableArray() }));
        byte[] badVertex = mesh.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(badVertex.AsSpan(0x350), ushort.MaxValue);
        Assert.Throws<InvalidDataException>(() => NativeBattleShivaShards.Initialize5C1EE0(frame, mesh with { Bytes = badVertex.ToImmutableArray() }));
        byte[] overflow = mesh.Bytes.ToArray();
        for (int vertex = 0; vertex < 3; vertex++)
        {
            for (int coordinate = 0; coordinate < 3; coordinate++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(overflow.AsSpan(8 + vertex * 8 + coordinate * 2), short.MaxValue);
            }
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleShivaShards.Initialize5C1EE0(frame, mesh with { Bytes = overflow.ToImmutableArray() }));
    }

    private static NativeBattleShivaEarlyTaskFrame Fixture()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        pool = pool with { Slots = pool.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, pool, new(1), new(new(0, 0, 0, 0), new(0, 0, 0, 0)));
    }

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] source = new byte[0x12C8];
        BinaryPrimitives.WriteUInt32LittleEndian(source, 0x340);
        short[][] vertices = [[-1, 3, 1000], [-1, 3, 1000], [-2, 4, 1000]];
        for (int vertex = 0; vertex < vertices.Length; vertex++)
        {
            for (int coordinate = 0; coordinate < 3; coordinate++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8 + vertex * 8 + coordinate * 2), vertices[vertex][coordinate]);
            }
        }
        int cursor = 0x340;
        for (int group = 0; group < 8; group++)
        {
            int count = group == 2 ? 102 : group == 6 ? 68 : 0;
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(cursor), (uint)count);
            cursor += 4;
            for (int index = 0; index < count; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(cursor + 4), 0);
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(cursor + 6), 2);
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(cursor + 8), 4);
                cursor += group == 2 ? 20 : 28;
            }
        }
        return new(0, 0xD2F1C4, source.ToImmutableArray());
    }

    private static ushort Word(NativeBattleEffectArena arena, int address) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(address, 2));
}
