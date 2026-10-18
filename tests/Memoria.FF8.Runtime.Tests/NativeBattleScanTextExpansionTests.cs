using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanTextExpansionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void Line_terminator_is_copied_without_extra_zero_or_suffix_clear(byte terminal)
    {
        NativeBattleScanTextFrame frame = SparseFrame();
        byte[] source = [0x19, 0xE7, terminal, 0x55];
        List<uint> reads = [];
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(frame, 0x100, -1, Services(pointer =>
        {
            reads.Add(pointer);
            return source[(int)(pointer - 0x100)];
        }));
        Assert.Equal(new byte[] { 0x19, 0xE7, terminal }, result.ExpandedContext128.Bytes.AsSpan().Slice(0, 3).ToArray());
        Assert.Equal(new uint[] { 0x100, 0x101, 0x102 }, reads);
        Assert.True(result.ExpandedContext128.Known.Take(3).All(value => value));
        Assert.False(result.ExpandedContext128.Known[3]);
        Assert.Equal((byte)0xA5, result.ExpandedContext128.Bytes[3]);
        Assert.Equal(frame.Formatting1D7660C, result.Formatting1D7660C);
    }

    [Fact]
    public void Null_source_writes_only_first_zero_and_reads_nothing()
    {
        NativeBattleScanTextFrame frame = SparseFrame();
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(frame, 0, -1, Services(_ => throw new InvalidDataException()));
        Assert.Equal((byte)0, result.ExpandedContext128.ReadByte(0));
        Assert.Equal(1, result.ExpandedContext128.Known.Count(value => value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Capacity_reads_next_source_before_writing_zero_at_actual_end(int maximum)
    {
        List<uint> reads = [];
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(SparseFrame(), 0x100, maximum, Services(pointer =>
        {
            reads.Add(pointer);
            return 0x44;
        }));
        Assert.Equal(maximum + 1, reads.Count);
        Assert.Equal((uint)(0x100 + maximum), reads[^1]);
        Assert.Equal((byte)0, result.ExpandedContext128.ReadByte(maximum));
        Assert.Equal(maximum + 1, result.ExpandedContext128.Known.Count(value => value));
    }

    [Theory]
    [InlineData(0x34, 0x1CFDC7Cu)]
    [InlineData(0x30, 0x1CFDC70u)]
    [InlineData(0x50, 0x1CFE754u)]
    public void Reached_name_substitution_uses_actual_mutable_name_bytes_and_overwrites_copied_terminator(byte operand, uint namePointer)
    {
        byte[] source = [3, operand, 0x46, 2];
        byte[] name = [0x40, 0x41, 0];
        NativeBattleScanTextExpansionServices services = new(pointer => pointer >= namePointer ? name[(int)(pointer - namePointer)] : source[(int)(pointer - 0x100)], selected =>
        {
            Assert.Equal(operand, selected);
            return namePointer;
        });
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(SparseFrame(), 0x100, -1, services);
        Assert.Equal(new byte[] { 0x40, 0x41, 0x46, 2 }, result.ExpandedContext128.Bytes.AsSpan().Slice(0, 4).ToArray());
        Assert.False(result.ExpandedContext128.Known[4]);
        NativeBattleScanTextFrame clone = JsonSerializer.Deserialize<NativeBattleScanTextFrame>(JsonSerializer.Serialize(SparseFrame()))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleScanTextExpansion.Expand4B8B30(clone, 0x100, -1, services)));
    }

    [Fact]
    public void Name_selector_support_is_owned_by_required_original_resolver()
    {
        byte[] source = [3, 0x40, 2];
        NativeBattleScanTextExpansionServices services = new(pointer => pointer == 0x200 ? (byte)0 : source[(int)(pointer - 0x100)], operand =>
        {
            Assert.Equal((byte)0x40, operand);
            return 0x200;
        });
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(SparseFrame(), 0x100, -1, services);
        Assert.Equal((byte)2, result.ExpandedContext128.ReadByte(0));
    }

    [Fact]
    public void Name_copy_crosses_requested_end_then_next_source_read_triggers_termination_at_end()
    {
        byte[] source = [3, 0x34, 0x47];
        byte[] name = [0x40, 0x41, 0x42, 0x43, 0];
        List<uint> reads = [];
        NativeBattleScanTextExpansionServices services = new(pointer =>
        {
            reads.Add(pointer);
            return pointer >= 0x200 ? name[(int)(pointer - 0x200)] : source[(int)(pointer - 0x100)];
        }, _ => 0x200);
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(SparseFrame(), 0x100, 2, services);
        Assert.Equal(new byte[] { 0x40, 0x41, 0, 0x43, 0 }, result.ExpandedContext128.Bytes.AsSpan().Slice(0, 5).ToArray());
        Assert.Equal(0x102u, reads[^1]);
        Assert.Equal(5, result.ExpandedContext128.Known.Count(value => value));
    }

    [Fact]
    public void Formatting_copies_its_operand_even_when_that_operand_is_a_terminator()
    {
        byte[] source = [6, 0, 0x44, 2];
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(SparseFrame(), 0x100, -1, Services(pointer => source[(int)(pointer - 0x100)]));
        Assert.Equal(source, result.ExpandedContext128.Bytes.AsSpan().Slice(0, 4).ToArray());
        Assert.False(result.ExpandedContext128.Known[4]);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(0x18)]
    [InlineData(0xE8)]
    public void Unclosed_expansion_commands_fail_instead_of_fabricating_text(byte opcode)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleScanTextExpansion.Expand4B8B30(SparseFrame(), 0x100, -1, Services(_ => opcode)));
    }

    [Fact]
    public void Default_limit_does_not_silently_truncate_to_127_to_avoid_actual_adjacent_write()
    {
        int reads = 0;
        Assert.Throws<InvalidDataException>(() => NativeBattleScanTextExpansion.Expand4B8B30(SparseFrame(), 0x100, -1, Services(_ =>
        {
            reads++;
            return 0x44;
        })));
        Assert.Equal(129, reads);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void Source_line_advance_consumes_formatting_and_returns_after_separator(byte separator)
    {
        byte[] source = [0x40, 6, 0xB3, 0x41, separator, 0x42];
        NativeBattleScanTextFrame frame = SparseFrame();
        NativeBattleScanTextLineStep result = NativeBattleScanTextExpansion.AdvanceLine4B8AC0(frame, 0x100, pointer => source[(int)(pointer - 0x100)]);
        Assert.Equal(0x105u, result.SourcePointer);
        Assert.Equal((byte)0xB3, result.Frame.Formatting1D7660C);
        Assert.Same(frame.ExpandedContext128, result.Frame.ExpandedContext128);
        Assert.Same(frame.Windows, result.Frame.Windows);
    }

    [Fact]
    public void Source_line_advance_handles_null_and_terminator_without_using_character_indices()
    {
        NativeBattleScanTextFrame frame = SparseFrame();
        Assert.Equal(0u, NativeBattleScanTextExpansion.AdvanceLine4B8AC0(frame, 0, _ => throw new InvalidDataException()).SourcePointer);
        List<uint> reads = [];
        NativeBattleScanTextLineStep end = NativeBattleScanTextExpansion.AdvanceLine4B8AC0(frame, 0x321, pointer =>
        {
            reads.Add(pointer);
            return 0;
        });
        Assert.Equal(0u, end.SourcePointer);
        Assert.Equal(new uint[] { 0x321 }, reads);
    }

    private static NativeBattleScanTextFrame SparseFrame() => ScanTextFixture.Frame() with { ExpandedContext128 = new(Enumerable.Repeat((byte)0xA5, 128).ToImmutableArray(), Enumerable.Repeat(false, 128).ToImmutableArray()) };
    private static NativeBattleScanTextExpansionServices Services(Func<uint, byte> reader) => new(reader, _ => throw new InvalidDataException("Unexpected name selector."));
}
