using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanElementTextTests
{
    [Fact]
    public void Full_element_row_fits_actual_system_font_advances_inside_clip_and_prefix_drawing_preserves_native_state()
    {
        string[] labels = ["Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy"];
        List<byte> source = FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes("Resists: ").ToList();
        byte space = FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes(" ")[0];
        for (int index = 0; index < labels.Length; index++)
        {
            source.Add(5);
            source.Add((byte)(0x5D + index));
            source.Add(space);
        }
        source.Add(0);
        NativeBattleScanTextFrame frame = Frame(255);
        frame = frame with { Windows = frame.Windows with { TextRows = frame.Windows.TextRows.SetItem(0, frame.Windows.Row(0).Write(0, new NativeBattleScanRect(16, 180, 296, 28).ToBytes())) } };
        string before = JsonSerializer.Serialize(frame);
        IReadOnlyList<NativeScanWindowText> full = NativeScanWindowTextProjection.Project(frame, Services(source.ToArray()), "en");
        Assert.True(Assert.Single(Assert.Single(full).Lines).FitWidth);
        SystemAdvanceCanvas canvas = new();
        NativeScanWindowDrawing.Draw(canvas, new UiTheme(), full, new UiRect(0, 0, 320, 224), 0, 1);
        string drawn = string.Concat(canvas.Runs.Select(run => run.Text));
        foreach (string label in labels)
        {
            Assert.Contains(label, drawn);
        }
        Assert.NotEmpty(canvas.Runs);
        Assert.All(canvas.Runs, run =>
        {
            Assert.True(run.Right <= run.Clip.Right + 0.05f, $"System-font run ends at{run.Right}, beyond clipped edge{run.Clip.Right}.");
            Assert.True(run.Size < 16);
        });
        Assert.Empty(canvas.Clips);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
        NativeBattleScanTextFrame prefix = frame with { Windows = frame.Windows with { TextRows = frame.Windows.TextRows.SetItem(0, frame.Windows.Row(0).WriteByte(0x20, 11)) } };
        string prefixBefore = JsonSerializer.Serialize(prefix);
        SystemAdvanceCanvas prefixCanvas = new();
        NativeScanWindowDrawing.Draw(prefixCanvas, new UiTheme(), NativeScanWindowTextProjection.Project(prefix, Services(source.ToArray()), "en"), new UiRect(0, 0, 320, 224), 0, 1);
        Assert.Equal("Resists: Fire", string.Concat(prefixCanvas.Runs.Select(run => run.Text)));
        Assert.All(prefixCanvas.Runs, run => Assert.Equal(16f, run.Size));
        Assert.Equal(prefixBefore, JsonSerializer.Serialize(prefix));
    }

    [Theory]
    [InlineData(0x5D, "Fire")]
    [InlineData(0x5E, "Ice")]
    [InlineData(0x5F, "Thunder")]
    [InlineData(0x60, "Earth")]
    [InlineData(0x61, "Poison")]
    [InlineData(0x62, "Wind")]
    [InlineData(0x63, "Water")]
    [InlineData(0x64, "Holy")]
    public void Element_pairs_project_to_semantic_system_font_labels_without_changing_native_bytes(byte operand, string expected)
    {
        NativeBattleScanTextFrame frame = Frame(2);
        string before = JsonSerializer.Serialize(frame);
        NativeScanWindowText window = Assert.Single(NativeScanWindowTextProjection.Project(frame, Services([5, operand, 0]), "en"));
        Assert.Equal(expected, window.Document.Text);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
        Assert.Equal(new byte[128], frame.ExpandedContext128.Bytes.ToArray());
    }

    [Theory]
    [InlineData(1, "A")]
    [InlineData(3, "AFire")]
    [InlineData(4, "AFireB")]
    public void Current_byte_prefix_reveals_only_consumed_text_and_complete_icon_pairs(int maximum, string expected)
    {
        byte a = FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes("A")[0];
        byte b = FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes("B")[0];
        NativeBattleScanTextFrame frame = Frame((byte)maximum);
        string before = JsonSerializer.Serialize(frame);
        NativeScanWindowText window = Assert.Single(NativeScanWindowTextProjection.Project(frame, Services([a, 5, 0x5D, b, 0]), "en"));
        Assert.Equal(expected, window.Document.Text);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    private static NativeBattleScanTextExpansionServices Services(byte[] source) => new(pointer => source[checked((int)(pointer - 0x100))], _ => throw new InvalidOperationException("Element labels do not use name pointers."));
    private static NativeBattleScanByteOwner Owner(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());

    private static NativeBattleScanTextFrame Frame(byte maximum)
    {
        NativeBattleScanByteOwner[] rows = Enumerable.Range(0, 8).Select(index => Owner(60).WriteByte(0x18, (byte)index)).ToArray();
        rows[0] = rows[0].WriteDword(8, 0x100).WriteDword(0x0C, 0x100).WriteByte(0x16, 1).WriteByte(0x17, 0x77).WriteWord(0x1A, 4096).WriteWord(0x1C, 4096).WriteByte(0x20, maximum).WriteByte(0x29, 0xFF).WriteByte(0x2A, 0xFF);
        ImmutableDictionary<byte, NativeBattleScanByteOwner> backends = Enumerable.Range(0, 8).ToImmutableDictionary(index => (byte)index, _ => Owner(64));
        backends = backends.SetItem(0, backends[0].WriteByte(0x34, 1).WriteByte(0x35, 1).Write(0x18, new NativeBattleScanRect(16, 180, 296, 28).ToBytes()).Write(0x24, new NativeBattleScanRect(22, 186, 284, 16).ToBytes()));
        return new(new(rows.ToImmutableArray(), backends), Owner(128), 0, 0, Owner(12));
    }

    private sealed record DrawnRun(string Text, float Size, float Right, UiRect Clip);

    private sealed class SystemAdvanceCanvas : IUiCanvas, INativeScanTextCanvas
    {
        public List<DrawnRun> Runs { get; } = [];
        public Stack<UiRect> Clips { get; } = new();

        public float ScanTextAdvance(string value, float size)
        {
            using SystemTextMeasurer measure = new(size, SystemFontCatalog.DefaultFamily);
            return measure.Measure(value);
        }

        public void ScanTextRun(string value, float size, float x, float y, UiColor color)
        {
            Runs.Add(new(value, size, x + ScanTextAdvance(value, size), Clips.Peek()));
        }

        public void PushClip(UiRect bounds)
        {
            Clips.Push(Clips.Count == 0 ? bounds : Clips.Peek().Intersect(bounds));
        }

        public void PopClip()
        {
            Clips.Pop();
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            throw new NotSupportedException();
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            throw new NotSupportedException();
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }
    }
}
