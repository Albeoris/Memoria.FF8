using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanTextProcessTests
{
    [Fact]
    public void Expanded_text_and_ui_flags_share_the_same_two_physical_bytes_across_disk_roundtrip()
    {
        NativeBattleScanTextProcess process = NativeBattleScanTextProcess.FromColdProcess();
        NativeBattleScanTextFrame frame = process.ReadFrame(0xABCD);
        Assert.Equal((byte)0xCD, frame.ExpandedContext128.ReadByte(0x2E));
        Assert.Equal((byte)0xAB, frame.ExpandedContext128.ReadByte(0x2F));
        frame = frame with { ExpandedContext128 = frame.ExpandedContext128.WriteByte(0x2D, 7).WriteWord(0x2E, 0x0123).WriteByte(0x30, 9) };
        process = process.CaptureFrame(frame, out ushort flags);
        Assert.Equal((ushort)0x0123, flags);
        process = JsonSerializer.Deserialize<NativeBattleScanTextProcess>(JsonSerializer.Serialize(process))!;
        frame = process.ReadFrame(flags);
        Assert.Equal((byte)7, frame.ExpandedContext128.ReadByte(0x2D));
        Assert.Equal((ushort)0x0123, frame.ExpandedContext128.ReadWord(0x2E));
        Assert.Equal((byte)9, frame.ExpandedContext128.ReadByte(0x30));
        Assert.Equal((ushort)0xFEDC, process.ReadFrame(0xFEDC).ExpandedContext128.ReadWord(0x2E));
        process.Validate();
    }

    [Fact]
    public void Capturing_text_does_not_replace_the_saved_context_pointer_or_initialization_state()
    {
        NativeBattleScanTextProcess process = NativeBattleScanTextProcess.FromColdProcess() with { CurrentContext1D76608 = 0x12345678, Initialized = true };
        NativeBattleScanTextFrame frame = process.ReadFrame(0) with { Formatting1D7660C = 0x37, SelectedWindow1D2B51C = 5 };
        NativeBattleScanTextProcess captured = process.CaptureFrame(frame, out _);
        Assert.Equal(0x12345678u, captured.CurrentContext1D76608);
        Assert.True(captured.Initialized);
        Assert.Equal((byte)0x37, captured.Formatting1D7660C);
        Assert.Equal((sbyte)5, captured.SelectedWindow1D2B51C);
    }
}
