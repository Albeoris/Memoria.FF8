using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid317SnapshotTests
{
    [Fact]
    public void Actual_IR2_and_one_CRT_draw_per_vertex_produce_unsigned_BYTE_cache()
    {
        NativeBattleAcid317SnapshotStep result = Capture(new(1), Temporary());
        Assert.Equal(new byte[] { 254, 4 }, result.Memory.Arena!.Read(CacheOffset, 2));
        NativeCrtRandomState expected = new NativeCrtRandomState(1).Draw(out _).Draw(out _);
        Assert.Equal(expected, result.Random);
        Assert.Equal(GeometryAddress + 26, U32(result.Temporary12, 4));
        Assert.Equal(CacheAddress + 2, U32(result.Temporary12, 8));
        Assert.Equal(0xA5A5A5A5u, U32(result.Temporary12, 0));
        Assert.Equal(-200, result.Memory.ScriptedColorRegisters!.Ir[1]);
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.True(result.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.Equal(0, result.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
    }

    [Fact]
    public void Linked_capture_continues_the_same_physical_cache_and_JSON_random_state()
    {
        NativeBattleAcid317SnapshotStep first = Capture(new(1), Temporary());
        NativeBattleAcid317SnapshotStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAcid317SnapshotStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleAcid317SnapshotStep Next(NativeBattleAcid317SnapshotStep current) => Capture(current.Random, current.Temporary12, current.Memory);
        NativeBattleAcid317SnapshotStep next = Next(first);
        Assert.Equal(CacheAddress + 4, U32(next.Temporary12, 8));
        Assert.Equal(first.Memory.Arena!.Read(CacheOffset, 2), next.Memory.Arena!.Read(CacheOffset, 2));
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    [Fact]
    public void Cache_extent_and_current_bone_owner_are_required_without_reading_faces()
    {
        Assert.Throws<InvalidDataException>(() => Capture(new(1), Temporary().Write(8, BitConverter.GetBytes(CacheAddress + 2048))));
        Assert.Throws<InvalidDataException>(() => NativeBattleAcid317Snapshot.Capture(Memory(), new(1), Temporary(), Read, GeometryAddress, [], CacheAddress, 2048));
        Assert.Throws<NotSupportedException>(() => Capture(new(1), Temporary(), Memory() with { GeometryRegisters = null }));
    }

    private const uint GeometryAddress = 0x1000000;
    private const int CacheOffset = 0x3F08;
    private const uint CacheAddress = NativeBattleEffectNodeMemory.ArenaAddress + CacheOffset;
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0xA5A5A5A5u)).Write(8, BitConverter.GetBytes(CacheAddress));
    private static NativeBattleAcid317SnapshotStep Capture(NativeCrtRandomState random, NativeBattleEffectArena temporary, NativeBattleEffectMemory? memory = null) => NativeBattleAcid317Snapshot.Capture(memory ?? Memory(), random, temporary, Read, GeometryAddress, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)], CacheAddress, 2048);
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static byte[] Read(int offset, int length)
    {
        byte[] geometry = new byte[26];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, 1);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(12), 2);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(16), 100);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(22), -200);
        return geometry.AsSpan(offset, length).ToArray();
    }
}
