using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanViewportProjectionTests
{
    [Fact]
    public void Presentation_materializes_cold_zero_clip_fields_without_mutating_current_native_owners()
    {
        NativeBattleScanTextFrame frame = Frame(new(170, 52, 136, 128));
        string before = JsonSerializer.Serialize(frame);
        Assert.Empty(Assert.Single(NativeScanWindowTextProjection.Project(frame, Services())).Lines);
        NativeScanWindowText window = Assert.Single(NativeScanWindowTextProjection.ProjectForViewport(frame, Services(), new(0, 0, 320, 224)));
        Assert.Equal("Sca", window.Document.Text);
        Assert.Equal(new NativeBattleScanRect(170, 52, 136, 128), window.Outer);
        Assert.Equal(new NativeBattleScanRect(176, 58, 124, 116), window.Inner);
        Assert.Equal(new NativeBattleScanRect(170, 52, 136, 128), window.OriginalRectangle);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
        Assert.Equal((ushort)0, frame.Windows.Backend(0).ReadWord(0x28));
        Assert.Equal((byte)8, window.BackendFlags38);
    }

    [Fact]
    public void Partial_viewport_intersection_keeps_original_text_origin_and_current_line_offsets()
    {
        NativeBattleScanTextFrame frame = Frame(new(-10, 20, 100, 80));
        string before = JsonSerializer.Serialize(frame);
        NativeScanWindowText window = Assert.Single(NativeScanWindowTextProjection.ProjectForViewport(frame, Services(), new(0, 0, 320, 224)));
        Assert.Equal(new NativeBattleScanRect(0, 20, 90, 80), window.Outer);
        Assert.Equal(new NativeBattleScanRect(0, 26, 84, 68), window.Inner);
        Assert.Equal(new NativeBattleScanRect(-10, 20, 100, 80), window.OriginalRectangle);
        NativeScanTextLine line = Assert.Single(window.Lines);
        Assert.Equal(2, line.X);
        Assert.Equal(2, line.Y);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void Fully_offscreen_backend_is_hidden_transiently_without_reading_text_or_changing_native_visibility()
    {
        NativeBattleScanTextFrame frame = Frame(new(330, 20, 100, 80));
        string before = JsonSerializer.Serialize(frame);
        Assert.Empty(NativeScanWindowTextProjection.ProjectForViewport(frame, new(_ => throw new InvalidDataException(), _ => throw new InvalidDataException()), new(0, 0, 320, 224)));
        Assert.Equal((byte)1, frame.Windows.Backend(0).ReadByte(0x35));
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    private static NativeBattleScanTextExpansionServices Services()
    {
        byte[] source = FF8Encoding.CreateEuropean().GetBytes("Scan").Append((byte)0).ToArray();
        return new(pointer => source[checked((int)(pointer - 0x100))], _ => throw new InvalidDataException());
    }

    private static NativeBattleScanByteOwner Owner(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());

    private static NativeBattleScanTextFrame Frame(NativeBattleScanRect rectangle)
    {
        NativeBattleScanByteOwner[] rows = Enumerable.Range(0, 8).Select(index => Owner(60).WriteByte(0x18, (byte)index)).ToArray();
        rows[0] = rows[0].Write(0, rectangle.ToBytes()).WriteDword(8, 0x100).WriteDword(0x0C, 0x100).WriteByte(0x16, 1).WriteByte(0x17, 0x77).WriteWord(0x1A, 4096).WriteWord(0x1C, 4096).WriteByte(0x20, 3).WriteByte(0x29, 0xFF).WriteByte(0x2A, 0xFF);
        NativeBattleScanRect inner = new((short)(rectangle.X + 6), (short)(rectangle.Y + 6), (short)(rectangle.Width - 12), (short)(rectangle.Height - 12));
        ImmutableDictionary<byte, NativeBattleScanByteOwner> backends = Enumerable.Range(0, 8).ToImmutableDictionary(index => (byte)index, _ => Owner(64));
        backends = backends.SetItem(0, backends[0].Write(8, rectangle.ToBytes()).Write(0x10, inner.ToBytes()).WriteByte(0x34, 1).WriteByte(0x35, 1).WriteByte(0x38, 8));
        return new(new(rows.ToImmutableArray(), backends), Owner(128), 0, 0, Owner(12));
    }
}
