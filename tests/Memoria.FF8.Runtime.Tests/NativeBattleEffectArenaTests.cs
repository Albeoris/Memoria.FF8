using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectArenaTests
{
    [Fact]
    public void OriginalClearProvesAllUnwrittenBytesZeroAndOverlappingWritesKeepOneOwner()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        Assert.All(arena.Read(0xFFFF0, 16), value => Assert.Equal((byte)0, value));
        arena = arena.Write(10, [1, 2, 3]).Write(12, [8, 9]).Write(8, [4]);
        Assert.Equal(new byte[] { 0, 4, 0, 1, 2, 8, 9, 0 }, arena.Read(7, 8));
        Assert.Equal(2, arena.Regions.Length);
        Assert.Equal(10, arena.Regions[1].Offset);
        Assert.Equal(new byte[] { 1, 2, 8, 9 }, arena.Regions[1].Data);
        arena.Validate();
    }

    [Fact]
    public void ArenaSnapshotRestoresWrittenRegionsWithoutStoringInventedUnknownBytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x30000, [5, 6, 7]);
        string json = JsonSerializer.Serialize(arena);
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(arena.Read(0x2FFFF, 5), restored.Read(0x2FFFF, 5));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleEffectArena>("{}"));
    }

    [Fact]
    public void ArenaDoesNotSpillAcrossTheNativeAllocationBoundary()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        Assert.Throws<InvalidDataException>(() => arena.Read(-1, 1));
        Assert.Throws<InvalidDataException>(() => arena.Write(0xFFFFF, [1, 2]));
        Assert.Throws<InvalidDataException>(() => arena.Read(0, int.MaxValue));
    }
}
