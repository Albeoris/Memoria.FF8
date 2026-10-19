using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectArenaTests
{
    [Fact]
    public void Successful_validation_keeps_record_equality_and_rejects_invalid_clones_independently()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(10, [1, 2, 3]);
        NativeBattleEffectArena equal = source with { };
        int hash = source.GetHashCode();
        string serialized = JsonSerializer.Serialize(source);
        source.Validate();
        source.Validate();
        Assert.Equal(equal, source);
        Assert.Equal(hash, source.GetHashCode());
        Assert.Equal(serialized, JsonSerializer.Serialize(source));
        NativeBattleEffectArena invalid = source with { Regions = [new(-1, [1])] };
        Assert.Throws<InvalidDataException>(() => invalid.Validate());
        Assert.Throws<InvalidDataException>(() => invalid.Validate());
        NativeBattleEffectArena unknown = source with { UnknownRegions = [new(10, 1), new(11, 1)] };
        Assert.Throws<InvalidDataException>(() => unknown.Read(0, 1));
    }

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

    [Fact]
    public void Writes_own_their_private_buffer_and_keep_prior_snapshots_immutable()
    {
        byte[] bytes = [1, 2, 3, 4];
        NativeBattleEffectArena first = NativeBattleEffectArena.Clear().Write(0x100, bytes);
        bytes[0] = 99;
        NativeBattleEffectArena second = first.Write(0x101, [8, 9]);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, first.Read(0x100, 4));
        Assert.Equal(new byte[] { 1, 8, 9, 4 }, second.Read(0x100, 4));
        byte[] exposedRead = second.Read(0x100, 4);
        exposedRead[0] = 77;
        Assert.Equal(new byte[] { 1, 8, 9, 4 }, second.Read(0x100, 4));
    }
}
