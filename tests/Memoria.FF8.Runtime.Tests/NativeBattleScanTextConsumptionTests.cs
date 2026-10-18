using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanTextConsumptionTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0x10, 0x10, 1)]
    [InlineData(0x18, 0x18, 1)]
    [InlineData(0x19, 0x1942, 2)]
    [InlineData(0x1F, 0x1F42, 2)]
    [InlineData(0x20, 0x20, 1)]
    [InlineData(6, 0x642, 2)]
    [InlineData(8, 0x842, 2)]
    public void Token_widths_and_zero_terminator_use_original_byte_offsets(byte token, int packed, byte offset)
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame();
        frame = frame with { ExpandedContext128 = frame.ExpandedContext128.Write(0, new byte[] { token, 0x42 }) };
        NativeBattleScanTextConsumeStep step = NativeBattleScanTextConsumption.Consume4B9170(frame, 0, ScanTextFixture.Unused());
        Assert.Equal(packed, step.Token);
        Assert.Equal(offset, step.Frame.Windows.Row(0).ReadByte(0x20));
        Assert.Equal(frame.Formatting1D7660C, step.Frame.Formatting1D7660C);
    }

    [Theory]
    [InlineData(2, 0x105u, 0x100u, 8, 10, 29)]
    [InlineData(1, 0x107u, 0x107u, 0, 0, 0)]
    [InlineData(7, 0x107u, 0x107u, 0, 0, 0)]
    public void Line_and_page_boundaries_scan_original_addresses_preserve_format_and_expand_exact_suffix(byte token, uint currentPointer, uint firstPointer, byte lines, byte pages, ushort scroll)
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame().WithRow(row => row.WriteByte(0x21, 7).WriteByte(0x22, 9).WriteWord(0x12, 29));
        frame = frame with { ExpandedContext128 = frame.ExpandedContext128.WriteByte(0, token) };
        byte[] text = [0x44, 6, 0xB3, 0x45, 2, 0x46, 7, 0];
        List<uint> expansions = [];
        NativeBattleScanTextServices services = new((current, pointer) =>
        {
            expansions.Add(pointer);
            return current with { ExpandedContext128 = current.ExpandedContext128.WriteByte(0, 0x55) };
        }, pointer => text[checked((int)(pointer - 0x100))], (_, _) => throw new InvalidDataException());
        NativeBattleScanTextConsumeStep step = NativeBattleScanTextConsumption.Consume4B9170(frame, 0, services);
        Assert.Equal(token, step.Token);
        Assert.Equal(currentPointer, step.Frame.Windows.Row(0).ReadDword(0x0C));
        Assert.Equal(firstPointer, step.Frame.Windows.Row(0).ReadDword(8));
        Assert.Equal(new[] { currentPointer }, expansions);
        Assert.Equal(lines, step.Frame.Windows.Row(0).ReadByte(0x21));
        Assert.Equal(pages, step.Frame.Windows.Row(0).ReadByte(0x22));
        Assert.Equal(scroll, step.Frame.Windows.Row(0).ReadWord(0x12));
        Assert.Equal((byte)0xB3, step.Frame.Formatting1D7660C);
        Assert.Equal((byte)0, step.Frame.Windows.Row(0).ReadByte(0x20));
        Assert.Equal((byte)0x55, step.Frame.ExpandedContext128.ReadByte(0));
        Assert.Equal(frame.ExpandedContext128.Bytes.AsSpan().Slice(1).ToArray(), step.Frame.ExpandedContext128.Bytes.AsSpan().Slice(1).ToArray());
        NativeBattleScanTextFrame clone = JsonSerializer.Deserialize<NativeBattleScanTextFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(NativeBattleScanTextConsumption.Consume4B9170(clone, 0, services)));
    }

    [Fact]
    public void Source_end_publishes_null_pointer_without_inventing_expansion_bytes()
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame();
        frame = frame with { ExpandedContext128 = frame.ExpandedContext128.WriteByte(0, 2) };
        NativeBattleScanTextServices services = new((current, pointer) =>
        {
            Assert.Equal(0u, pointer);
            return current;
        }, _ => 0, (_, _) => throw new InvalidDataException());
        NativeBattleScanTextConsumeStep result = NativeBattleScanTextConsumption.Consume4B9170(frame, 0, services);
        Assert.Equal(0u, result.Frame.Windows.Row(0).ReadDword(0x0C));
        Assert.Equal(0x100u, result.Frame.Windows.Row(0).ReadDword(8));
        Assert.Equal(frame.ExpandedContext128, result.Frame.ExpandedContext128);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void Already_null_source_expands_null_without_reading_original_address_zero(byte token)
    {
        NativeBattleScanTextFrame frame = ScanTextFixture.Frame().WithRow(row => row.WriteDword(8, 0).WriteDword(0x0C, 0));
        frame = frame with { ExpandedContext128 = frame.ExpandedContext128.WriteByte(0, token) };
        int expansions = 0;
        NativeBattleScanTextServices services = ScanTextFixture.Unused() with
        {
            Expand4B8B30 = (current, pointer) =>
            {
                Assert.Equal(0u, pointer);
                expansions++;
                return current;
            }
        };
        NativeBattleScanTextConsumeStep result = NativeBattleScanTextConsumption.Consume4B9170(frame, 0, services);
        Assert.Equal(1, expansions);
        Assert.Equal(0u, result.Frame.Windows.Row(0).ReadDword(0x0C));
        Assert.Equal(0u, result.Frame.Windows.Row(0).ReadDword(8));
        Assert.Equal((byte)0, result.Frame.Windows.Row(0).ReadByte(0x20));
    }
}
