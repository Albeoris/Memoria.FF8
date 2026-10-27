using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanWindowTextProjectionTests
{
    [Fact]
    public void Full_native_lines_and_current_byte_prefix_become_semantic_system_text_without_mutating_state()
    {
        byte[] source = Text("Full").Concat(new byte[] { 2 }).Concat(Text("Prefix")).Append((byte)0).ToArray();
        NativeBattleScanTextFrame frame = Frame().WithRow(row => row.WriteByte(0x22, 1).WriteByte(0x20, 2).WriteByte(0x21, 1));
        string before = JsonSerializer.Serialize(frame);
        NativeScanWindowText window = Assert.Single(NativeScanWindowTextProjection.Project(frame, Services(source), "en"));
        Assert.Equal("Full\nPr", window.Document.Text);
        Assert.Equal(new[] { 0, 1 }, window.Lines.Select(line => line.Line.SourceLineIndex));
        Assert.Equal(new[] { 2, 18 }, window.Lines.Select(line => line.Y));
        Assert.Equal(new[] { 2, 2 }, window.Lines.Select(line => line.X));
        Assert.Equal(new NativeBattleScanRect(170, 52, 136, 128), window.Outer);
        Assert.Equal(new NativeBattleScanRect(176, 58, 124, 116), window.Inner);
        Assert.Equal((ushort)4096, window.Progress);
        Assert.Equal((ushort)4096, window.Intensity);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void Actual_name_substitution_and_style_controls_use_existing_dialogue_runs()
    {
        byte[] source = new byte[] { 6, 0x24, 3, 0x34, 2 }.Concat(Text("B")).Append((byte)0).ToArray();
        byte[] name = Text("SavedName").Append((byte)0).ToArray();
        NativeBattleScanTextFrame frame = Frame().WithRow(row => row.WriteByte(0x22, 1).WriteByte(0x20, 1));
        NativeBattleScanTextExpansionServices services = new(pointer => pointer >= 0x200 ? name[(int)(pointer - 0x200)] : source[(int)(pointer - 0x100)], operand =>
        {
            Assert.Equal((byte)0x34, operand);
            return 0x200;
        });
        NativeScanWindowText window = Assert.Single(NativeScanWindowTextProjection.Project(frame, services, "en"));
        Assert.Equal("SavedName\nB", window.Document.Text);
        Assert.All(window.Lines.SelectMany(line => line.Line.Runs), run => Assert.Equal(Color.Lime, run.Color));
    }

    [Fact]
    public void Native_scroll_skips_only_lines_above_negative_sixteen_and_keeps_format_and_selection_indent()
    {
        byte[] source = new byte[] { 6, 0x24 }.Concat(Text("A")).Append((byte)2).Concat(Text("B")).Append((byte)2).Concat(Text("C")).Append((byte)0).ToArray();
        NativeBattleScanTextFrame frame = Frame().WithRow(row => row.WriteWord(0x12, 35).WriteByte(0x22, 2).WriteByte(0x20, 1).WriteByte(0x29, 2).WriteByte(0x2A, 2));
        NativeScanWindowText window = Assert.Single(NativeScanWindowTextProjection.Project(frame, Services(source), "en"));
        NativeScanTextLine line = Assert.Single(window.Lines);
        Assert.Equal("C", line.Line.Text);
        Assert.Equal(2, line.Line.SourceLineIndex);
        Assert.Equal(-1, line.Y);
        Assert.Equal(34, line.X);
        Assert.Equal(Color.Lime, Assert.Single(line.Line.Runs).Color);
    }

    [Theory]
    [InlineData(11, 116, 1, 0)]
    [InlineData(124, 11, 1, 0)]
    [InlineData(124, 116, 0, 0)]
    [InlineData(124, 116, 1, 1)]
    public void Text_uses_actual_clipped_inner_size_and_active_row_gate_even_during_partial_opening(ushort width, ushort height, byte active, int lineCount)
    {
        NativeBattleScanTextFrame frame = Frame().WithRow(row => row.WriteWord(0x1C, 2048).WriteByte(0x16, active).WriteByte(0x20, 1));
        NativeBattleScanByteOwner backend = frame.Windows.Backend(0).WriteWord(0x28, width).WriteWord(0x2A, height);
        frame = frame with { Windows = frame.Windows with { BackendRows = frame.Windows.BackendRows.SetItem(0, backend) } };
        NativeScanWindowText window = Assert.Single(NativeScanWindowTextProjection.Project(frame, Services(Text("A").Append((byte)0).ToArray()), "en"));
        Assert.Equal(lineCount, window.Lines.Count);
        Assert.Equal((ushort)2048, window.Progress);
    }

    [Theory]
    [InlineData(0x34, 0)]
    [InlineData(0x35, 0)]
    [InlineData(0x36, 1)]
    public void Disabled_hidden_or_suppressed_backend_does_not_read_text_or_fabricate_a_window(int offset, byte value)
    {
        NativeBattleScanTextFrame frame = Frame();
        frame = frame with { Windows = frame.Windows with { BackendRows = frame.Windows.BackendRows.SetItem(0, frame.Windows.Backend(0).WriteByte(offset, value)) } };
        Assert.Empty(NativeScanWindowTextProjection.Project(frame, new(_ => throw new InvalidDataException(), _ => throw new InvalidDataException()), "en"));
    }

    [Fact]
    public void Zero_current_prefix_does_not_reveal_unconsumed_source_text_and_restored_projection_matches()
    {
        NativeBattleScanTextFrame frame = Frame();
        NativeBattleScanTextExpansionServices services = Services(Text("Hidden").Append((byte)0).ToArray());
        NativeScanWindowText window = Assert.Single(NativeScanWindowTextProjection.Project(frame, services, "en"));
        Assert.Equal("", window.Document.Text);
        NativeBattleScanTextFrame restored = JsonSerializer.Deserialize<NativeBattleScanTextFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(window), JsonSerializer.Serialize(Assert.Single(NativeScanWindowTextProjection.Project(restored, services, "en"))));
    }

    private static byte[] Text(string value) => FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes(value);
    private static NativeBattleScanTextExpansionServices Services(byte[] source) => new(pointer => source[checked((int)(pointer - 0x100))], _ => throw new InvalidDataException("Unexpected name selector."));
    private static NativeBattleScanByteOwner Owner(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
    private static NativeBattleScanTextFrame Frame()
    {
        NativeBattleScanByteOwner[] rows = Enumerable.Range(0, 8).Select(index => Owner(60).WriteByte(0x18, (byte)index)).ToArray();
        rows[0] = rows[0].WriteDword(8, 0x100).WriteDword(0x0C, 0x100).WriteByte(0x16, 1).WriteByte(0x17, 0x77).WriteWord(0x1A, 4096).WriteWord(0x1C, 4096).WriteByte(0x29, 0xFF).WriteByte(0x2A, 0xFF);
        ImmutableDictionary<byte, NativeBattleScanByteOwner> backends = Enumerable.Range(0, 8).ToImmutableDictionary(index => (byte)index, _ => Owner(64));
        backends = backends.SetItem(0, backends[0].WriteByte(0x34, 1).WriteByte(0x35, 1).WriteByte(0x37, 6).Write(0x18, new NativeBattleScanRect(170, 52, 136, 128).ToBytes()).Write(0x24, new NativeBattleScanRect(176, 58, 124, 116).ToBytes()));
        return new(new(rows.ToImmutableArray(), backends), Owner(128), 0xA5, 0, Owner(12));
    }
}

internal static class NativeScanWindowTextTestInputs
{
    public static NativeBattleScanTextFrame WithRow(this NativeBattleScanTextFrame frame, Func<NativeBattleScanByteOwner, NativeBattleScanByteOwner> update) => frame with { Windows = frame.Windows with { TextRows = frame.Windows.TextRows.SetItem(0, update(frame.Windows.Row(0))) } };
}
