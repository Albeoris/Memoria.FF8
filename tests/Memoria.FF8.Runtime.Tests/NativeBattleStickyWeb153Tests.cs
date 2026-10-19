using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStickyWeb153Tests
{
    [Fact]
    public void Rejected_strands_keep_their_attempted_tag_and_native_trailing_reservation()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, -100, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        ImmutableArray<NativeBattleVector> samples = Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 16).ToImmutableArray();
        NativeBattleGfGeometryBatch batch = NativeBattleStickyWeb153Drawing.Strips(samples, samples, matrix, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x100);
        Assert.Equal(36, batch.AllocationBytes);
        Assert.Equal(14, batch.Attempts.Length);
        Assert.All(batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 36);
        NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(arena, "243BE6C", 0, 0x10000, NativeBattleLogicalOrdering.Clear(), batch, 36);
        Assert.Equal(36, written.Cursor);
        Assert.Equal(new byte[] { 0, 0, 0, 8 }, written.Arena.Read(0, 4));
        Assert.Throws<NotSupportedException>(() => written.Arena.Read(4, 32));
    }

    [Fact]
    public void Accepted_strands_link_both_color_planes_and_reserve_an_unwritten_tail()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        ImmutableArray<NativeBattleVector> first = Enumerable.Range(0, 16).Select(index => new NativeBattleVector((short)(index * 10), -50, 0)).ToImmutableArray();
        ImmutableArray<NativeBattleVector> second = first.Select(point => point with { Y = 50 }).ToImmutableArray();
        NativeBattleGfGeometryBatch batch = NativeBattleStickyWeb153Drawing.Strips(first, second, matrix, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x100);
        Assert.Equal(28, batch.Attempts.Length);
        Assert.Equal(14 * 72 + 36, batch.AllocationBytes);
        Assert.Equal(0x3A000000u, batch.Attempts[1].Primitive!.Words[1]);
        NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(NativeBattleEffectArena.Clear().MarkUnknown(0, 1100), "243BE6C", 0, 0x10000, NativeBattleLogicalOrdering.Clear(), batch, 36);
        Assert.Equal(28, written.Ordering.Proxies.Length);
        Assert.Throws<NotSupportedException>(() => written.Arena.Read(14 * 72, 36));
    }
}
