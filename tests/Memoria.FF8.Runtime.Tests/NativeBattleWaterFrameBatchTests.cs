using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterFrameBatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Private_segments_match_independent_execution_at_each_explicit_capture_and_after_later_failure(bool fail)
    {
        NativeBattleWaterExecutionFrame source = NativeBattleStorageSnapshots.Capture(NativeBattleIfritCallbackTests.Create());
        string original = JsonSerializer.Serialize(source);
        var expected = Run(NativeBattleStorageSnapshots.Restore(source), false, fail);
        var actual = Run(NativeBattleStorageSnapshots.Restore(source), true, fail);
        Assert.Equal(expected.Publications, actual.Publications);
        Assert.Equal(expected.Error, actual.Error);
        Assert.Equal(JsonSerializer.Serialize(expected.Published), JsonSerializer.Serialize(actual.Published));
        Assert.Null(actual.Published.Arena.Transaction);
        Assert.Null(actual.Published.Core.Core.Transaction);
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void Explicitly_captured_publications_remain_immutable_during_the_next_private_segment()
    {
        NativeBattleWaterExecutionFrame source = NativeBattleIfritCallbackTests.Create();
        using NativeBattleWaterFrameBatch batch = new();
        NativeBattleWaterExecutionFrame current = batch.Publish(Store(batch.Begin(source), 1));
        NativeBattleWaterExecutionFrame published = NativeBattleStorageSnapshots.Capture(current);
        string original = JsonSerializer.Serialize(published);
        current = Store(batch.Begin(current), 2);
        Assert.Equal(original, JsonSerializer.Serialize(published));
        current = batch.Publish(current);
        Assert.NotEqual(original, SnapshotJson(current));
        Assert.Equal(original, JsonSerializer.Serialize(published));
    }

    [Fact]
    public void Existing_transactions_and_temporary_parent_remain_active_after_nested_segment_publication()
    {
        NativeBattleWaterExecutionFrame source = NativeBattleIfritCallbackTests.Create();
        NativeBattleWaterTemporaryFrame before = NativeBattleStorageSnapshots.Capture(source.TemporaryFrame);
        using NativeBattleEffectArenaTransaction arena = new(source.Arena);
        using NativeBattleEffectArenaTransaction core = new(source.Core.Core);
        using NativeBattleWaterTemporaryFrame.PrivateWrite temporary = source.TemporaryFrame.BeginPrivateWrite();
        NativeBattleWaterExecutionFrame borrowed = source with { Arena = arena.View, Core = source.Core with { Core = core.View }, TemporaryFrame = temporary.View };
        using (NativeBattleWaterFrameBatch batch = new())
        {
            NativeBattleWaterExecutionFrame result = batch.Publish(Store(batch.Begin(borrowed), 7));
            Assert.Same(arena, result.Arena.Transaction);
            Assert.Same(core, result.Core.Core.Transaction);
            Assert.Same(temporary.View, result.TemporaryFrame);
            Assert.Equal((ushort)7, result.ReadTemporaryWord(0xE0));
        }
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source.TemporaryFrame)));
        arena.View.Write(0x6010, [11]);
        core.View.Write(0x1800, [13]);
        temporary.View.WriteUInt16(0xE0, 17);
        Assert.Equal((byte)11, arena.Freeze().Read(0x6010, 1)[0]);
        Assert.Equal((byte)13, core.Freeze().Read(0x1800, 1)[0]);
        Assert.Same(source.TemporaryFrame, temporary.Publish());
        Assert.Equal((ushort)17, source.ReadTemporaryWord(0xE0));
    }

    [Fact]
    public void Disposing_latest_segment_restores_accepted_temporary_hidden_bytes_and_knownness()
    {
        NativeBattleWaterExecutionFrame source = NativeBattleIfritCallbackTests.Create();
        NativeBattleWaterTemporaryFrame accepted;
        using (NativeBattleWaterFrameBatch batch = new())
        {
            NativeBattleWaterExecutionFrame current = batch.Begin(source);
            current.TemporaryFrame.WriteWithKnownness(0xE0, [0xA5, 0x5A], [true, false]);
            current = batch.Publish(current);
            accepted = NativeBattleStorageSnapshots.Capture(current.TemporaryFrame);
            current = batch.Begin(current);
            current.WriteTemporaryWord(0xE0, 0x1234);
        }
        Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source.TemporaryFrame)));
        Assert.Throws<NotSupportedException>(() => source.ReadTemporaryWord(0xE0));
    }

    private static (NativeBattleWaterExecutionFrame Published, string[] Publications, string? Error) Run(NativeBattleWaterExecutionFrame source, bool batched, bool fail)
    {
        using NativeBattleWaterFrameBatch batch = new();
        NativeBattleWaterExecutionFrame current = source;
        NativeBattleWaterExecutionFrame published = NativeBattleStorageSnapshots.Capture(source);
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
                published = NativeBattleStorageSnapshots.Capture(current);
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
        Assert.Equal(new byte[] { (byte)segment, 4, 5 }, frame.Arena.Read(0x6000, 3));
        frame = frame with { Core = frame.Core.Write(0x2798700, frame.Arena.Read(0x6000, 3)).Write(0x2798701, [7]) };
        Assert.Equal(new byte[] { (byte)segment, 7, 5 }, frame.Core.Read(0x2798700, 3));
        frame = frame with { Random = frame.Random.Draw(out _) };
        return frame.WriteTemporaryWord(0xE0, checked((ushort)segment));
    }

    private static string SnapshotJson(NativeBattleWaterExecutionFrame frame) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame));
}
