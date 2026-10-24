using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterFrameBatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Private_segments_match_immutable_execution_at_each_publication_and_after_later_failure(bool fail)
    {
        NativeBattleWaterExecutionFrame source = NativeBattleIfritCallbackTests.Create();
        string original = JsonSerializer.Serialize(source);
        var expected = Run(source, false, fail);
        var actual = Run(source, true, fail);
        Assert.Equal(expected.Publications, actual.Publications);
        Assert.Equal(expected.Error, actual.Error);
        Assert.Equal(JsonSerializer.Serialize(expected.Published), JsonSerializer.Serialize(actual.Published));
        Assert.Null(actual.Published.Arena.Transaction);
        Assert.Null(actual.Published.Core.Core.Transaction);
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void Earlier_published_aliases_remain_immutable_during_the_next_private_segment()
    {
        NativeBattleWaterExecutionFrame source = NativeBattleIfritCallbackTests.Create();
        using NativeBattleWaterFrameBatch batch = new();
        NativeBattleWaterExecutionFrame current = batch.Begin(source);
        current = Store(current, 1);
        NativeBattleWaterExecutionFrame published = batch.Publish(current);
        string original = JsonSerializer.Serialize(published);
        current = Store(batch.Begin(published), 2);
        Assert.Equal(original, JsonSerializer.Serialize(published));
        Assert.NotEqual(original, JsonSerializer.Serialize(batch.Publish(current)));
        Assert.Equal(original, JsonSerializer.Serialize(published));
    }

    [Fact]
    public void Existing_transactions_are_borrowed_and_remain_active_after_segment_publication_and_disposal()
    {
        NativeBattleWaterExecutionFrame source = NativeBattleIfritCallbackTests.Create();
        using NativeBattleEffectArenaTransaction arena = new(source.Arena);
        using NativeBattleEffectArenaTransaction core = new(source.Core.Core);
        NativeBattleWaterExecutionFrame borrowed = source with { Arena = arena.View, Core = source.Core with { Core = core.View } };
        using (NativeBattleWaterFrameBatch batch = new())
        {
            NativeBattleWaterExecutionFrame result = batch.Publish(Store(batch.Begin(borrowed), 7));
            Assert.Same(arena, result.Arena.Transaction);
            Assert.Same(core, result.Core.Core.Transaction);
        }
        arena.View.Write(0x6010, [11]);
        core.View.Write(0x1800, [13]);
        Assert.Equal((byte)11, arena.Freeze().Read(0x6010, 1)[0]);
        Assert.Equal((byte)13, core.Freeze().Read(0x1800, 1)[0]);
    }

    private static (NativeBattleWaterExecutionFrame Published, string[] Publications, string? Error) Run(NativeBattleWaterExecutionFrame source, bool batched, bool fail)
    {
        using NativeBattleWaterFrameBatch batch = new();
        NativeBattleWaterExecutionFrame current = source;
        NativeBattleWaterExecutionFrame published = source;
        List<string> publications = [];
        try
        {
            for (int segment = 1; segment <= 3; segment++)
            {
                if (batched)
                {
                    current = batch.Begin(current);
                }
                current = Store(current, segment);
                if (fail && segment == 2)
                {
                    throw new NotSupportedException("Selected unpublished segment boundary.");
                }
                if (batched)
                {
                    current = batch.Publish(current);
                }
                published = current;
                Assert.Null(published.Arena.Transaction);
                Assert.Null(published.Core.Core.Transaction);
                publications.Add(JsonSerializer.Serialize(published));
            }
            return (published, publications.ToArray(), null);
        }
        catch (NotSupportedException error)
        {
            return (published, publications.ToArray(), error.Message);
        }
    }

    private static NativeBattleWaterExecutionFrame Store(NativeBattleWaterExecutionFrame frame, int segment)
    {
        frame = frame with { Arena = frame.Arena.Write(0x6000, [(byte)segment, 2, 3]).Write(0x6001, [4, 5]).MarkUnknown(0x6008, 1) };
        byte[] bytes = frame.Arena.Read(0x6000, 3);
        Assert.Equal(new byte[] { (byte)segment, 4, 5 }, bytes);
        frame = frame with { Core = frame.Core.Write(0x2798700, bytes).Write(0x2798701, [7]) };
        Assert.Equal(new byte[] { (byte)segment, 7, 5 }, frame.Core.Read(0x2798700, 3));
        frame = frame with { Random = frame.Random.Draw(out _) };
        return frame.WriteTemporaryWord(0xE0, checked((ushort)segment));
    }
}
