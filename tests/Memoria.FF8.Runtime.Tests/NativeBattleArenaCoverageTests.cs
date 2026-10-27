using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleArenaCoverageTests
{
    [Fact]
    public void Disjoint_physical_stores_can_insert_before_between_and_after_retained_ranges()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateUnknown(8192);
        List<(int Offset, int Length)> expected = [];
        for (int index = 0; index < 128; index++)
        {
            int offset = 64 + index * 32;
            arena.Write(offset, [1, 2, 3, 4]);
            expected.Add((offset, 4));
        }
        foreach (int offset in new[] { 4, 64 + 10 * 32 + 12, 64 + 128 * 32 })
        {
            arena.Write(offset, [5, 6, 7, 8]);
            expected.Add((offset, 4));
        }
        Assert.Equal(expected.OrderBy(range => range.Offset), Ranges(arena));
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, arena.Read(4, 4));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, arena.Read(64 + 127 * 32, 4));
        Assert.False(arena.ReadSnapshot(64 + 10 * 32 + 8, 1).Known[0]);
    }

    [Theory]
    [InlineData(12, 4, "12:8,24:4,40:4")]
    [InlineData(20, 4, "16:12,40:4")]
    [InlineData(20, 20, "16:28")]
    [InlineData(16, 4, "16:4,24:4,40:4")]
    [InlineData(17, 2, "16:4,24:4,40:4")]
    [InlineData(18, 8, "16:12,40:4")]
    [InlineData(28, 12, "16:4,24:20")]
    [InlineData(44, 4, "16:4,24:4,40:8")]
    [InlineData(8, 48, "8:48")]
    public void Actual_stores_merge_adjacent_and_overlapping_footprints_at_exact_boundaries(int offset, int length, string expected)
    {
        NativeBattleEffectArena arena = Seed();
        arena.Write(offset, new byte[length]);
        Assert.Equal(expected, Footprint(arena));
        arena.MarkUnknown(offset, length);
        Assert.Equal(expected, Footprint(arena));
        Assert.All(arena.ReadSnapshot(offset, length).Known, known => Assert.False(known));
    }

    [Fact]
    public void Committed_nested_merges_remain_reversible_by_the_original_physical_checkpoint()
    {
        NativeBattleEffectArena arena = Seed();
        arena.MarkUnknown(17, 1);
        string before = JsonSerializer.Serialize(arena.Capture());
        arena.Working.BeginCheckpoint();
        arena.Write(20, new byte[20]);
        arena.Working.BeginNestedCheckpoint();
        arena.Write(4, [9, 8, 7, 6]);
        arena.Write(44, [5, 4, 3, 2]);
        arena.MarkUnknown(24, 4);
        arena.Working.CommitNestedCheckpoint();
        Assert.Equal("4:4,16:32", Footprint(arena));
        arena.Working.RollbackCheckpoint();
        Assert.Equal(before, JsonSerializer.Serialize(arena.Capture()));
        Assert.Equal("16:4,24:4,40:4", Footprint(arena));
    }

    private static NativeBattleEffectArena Seed() => NativeBattleEffectArena.CreateUnknown(128).Write(16, [1, 2, 3, 4]).Write(24, [5, 6, 7, 8]).Write(40, [9, 10, 11, 12]);
    private static string Footprint(NativeBattleEffectArena arena) => string.Join(",", Ranges(arena).Select(range => $"{range.Offset}:{range.Length}"));

    private static List<(int Offset, int Length)> Ranges(NativeBattleEffectArena arena)
    {
        List<(int Offset, int Length)> ranges = [];
        foreach (NativeBattleEffectStorageRange range in arena.EnumerateDataRanges())
        {
            ranges.Add((range.Offset, range.Length));
        }
        return ranges;
    }
}
