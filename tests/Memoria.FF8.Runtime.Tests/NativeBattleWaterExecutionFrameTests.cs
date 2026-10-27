using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterExecutionFrameTests
{
    [Fact]
    public void Temporary_writes_match_scalar_memory_and_preserve_prior_frames_with_overlapping_input()
    {
        NativeBattleWaterExecutionFrame original = Fixture();
        string originalJson = JsonSerializer.Serialize(original);
        NativeBattleWaterExecutionFrame actual = original;
        NativeBattleWaterExecutionFrame expected = original;
        for (int index = 0; index < 64; index++)
        {
            int source = index * 5 % 350;
            int destination = (source + 3) % 350;
            ReadOnlySpan<byte> input = actual.TemporaryFrame.Bytes.AsSpan().Slice(source, 24);
            NativeBattleWaterExecutionFrame previous = actual;
            string previousJson = JsonSerializer.Serialize(previous);
            expected = ScalarWrite(expected, destination, input);
            actual = actual.WriteTemporary(destination, input);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
            Assert.Equal(previousJson, JsonSerializer.Serialize(previous));
            Assert.Equal(original.TemporaryFrame.Address, actual.TemporaryFrame.Address);
            Assert.Equal(original.ModuleViewBackup, actual.ModuleViewBackup);
        }
        Assert.Equal(originalJson, JsonSerializer.Serialize(original));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(actual))!;
        Assert.Equal(JsonSerializer.Serialize(actual.WriteTemporaryDword(380, 0xFEDCBA98)), JsonSerializer.Serialize(restored.WriteTemporaryDword(380, 0xFEDCBA98)));
    }

    [Fact]
    public void Known_identical_stores_reuse_owners_but_identical_unknown_bytes_become_known()
    {
        NativeBattleWaterExecutionFrame original = Fixture();
        NativeBattleWaterExecutionFrame known = original.WriteTemporary(100, original.TemporaryFrame.Bytes.AsSpan().Slice(100, 8));
        Assert.NotSame(original, known);
        Assert.All(known.TemporaryFrame.Known.AsSpan().Slice(100, 8).ToArray(), value => Assert.True(value));
        Assert.Equal(original.TemporaryFrame.Bytes.ToArray(), known.TemporaryFrame.Bytes.ToArray());
        Assert.Same(known, known.WriteTemporary(100, known.TemporaryFrame.Bytes.AsSpan().Slice(100, 8)));
        Assert.Same(known, known.WriteTemporary(384, []));
        NativeBattleWaterExecutionFrame changed = known.WriteTemporaryDword(100, 0xFEDCBA98).WriteTemporaryWord(104, 0x8765).WriteTemporaryByte(106, 0xA5);
        Assert.Equal(known.TemporaryFrame.Known, changed.TemporaryFrame.Known);
        Assert.Equal(0xFEDCBA98u, changed.ReadTemporaryDword(100));
        Assert.Equal((ushort)0x8765, changed.ReadTemporaryWord(104));
        Assert.Equal(0xA5, changed.ReadTemporaryByte(106));
        Assert.False(changed.TemporaryFrame.Known[99]);
        Assert.False(changed.TemporaryFrame.Known[108]);
        byte[] independent = changed.ReadTemporary(100, 8);
        independent[0] = 0;
        Assert.Equal(0xFEDCBA98u, changed.ReadTemporaryDword(100));
        Assert.Throws<NotSupportedException>(() => changed.ReadTemporaryDword(98));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(385, 0)]
    [InlineData(384, 1)]
    [InlineData(383, 2)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(0, int.MaxValue)]
    public void Temporary_access_refuses_invalid_bounds_before_consuming_bytes(int offset, int length)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        Assert.Throws<InvalidDataException>(() => frame.ReadTemporary(offset, length));
        if (length >= 0 && length <= 2)
        {
            Assert.Throws<InvalidDataException>(() => frame.WriteTemporary(offset, new byte[length]));
        }
        Assert.Throws<InvalidDataException>(() => frame.ReadTemporaryByte(384));
        Assert.Throws<InvalidDataException>(() => frame.ReadTemporaryWord(383));
        Assert.Throws<InvalidDataException>(() => frame.ReadTemporaryDword(381));
        Assert.Throws<NotSupportedException>(() => frame.ReadTemporaryByte(0));
        Assert.Throws<NotSupportedException>(() => frame.ReadTemporaryWord(0));
        Assert.Throws<NotSupportedException>(() => frame.ReadTemporaryDword(0));
        Assert.Empty(frame.ReadTemporary(384, 0));
        NativeBattleWaterExecutionFrame malformed = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.RemoveAt(383) } };
        Assert.Throws<InvalidDataException>(() => malformed.ReadTemporary(0, 0));
        Assert.Throws<InvalidDataException>(() => malformed.WriteTemporary(0, []));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        return frame with { TemporaryFrame = frame.TemporaryFrame with { Bytes = Enumerable.Range(0, 384).Select(value => unchecked((byte)value)).ToImmutableArray(), Known = Enumerable.Repeat(false, 384).ToImmutableArray() }, ModuleViewBackup = [1, 2, 3] };
    }

    private static NativeBattleWaterExecutionFrame ScalarWrite(NativeBattleWaterExecutionFrame frame, int offset, ReadOnlySpan<byte> input)
    {
        byte[] bytes = frame.TemporaryFrame.Bytes.ToArray();
        bool[] known = frame.TemporaryFrame.Known.ToArray();
        input.CopyTo(bytes.AsSpan(offset));
        Array.Fill(known, true, offset, input.Length);
        return frame with { TemporaryFrame = frame.TemporaryFrame with { Bytes = bytes.ToImmutableArray(), Known = known.ToImmutableArray() } };
    }
}
