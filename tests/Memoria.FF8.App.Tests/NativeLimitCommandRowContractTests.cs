using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeLimitCommandRowContractTests
{
    [Fact]
    public void Actual_ui_service_continues_after_JSON_and_masks_input_without_fabricating_a_publication_edge()
    {
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize() with { Menu957 = 4, HeldButtons16 = 0x2000, Frame33 = false, CommandRowSelection = new(0, 0, 4, true) };
        ui = ui.AdvanceCommandRow4BB9E0();
        Assert.Equal(8, ui.CommandRow!.Offset26);
        Assert.False(ui.Frame33);
        NativeBattleUiServices restored = JsonSerializer.Deserialize<NativeBattleUiServices>(JsonSerializer.Serialize(ui))!;
        Assert.Equal(ui.CommandRow, restored.CommandRow);
        Assert.Equal(16, restored.AdvanceCommandRow4BB9E0().CommandRow!.Offset26);
        Assert.Equal(0, (restored with { HeldButtons16 = 0 }).AdvanceCommandRow4BB9E0().CommandRow!.Offset26);
        Assert.Equal(8, (restored with { Modal701 = 1, Modal702 = 1 }).AdvanceCommandRow4BB9E0().CommandRow!.Offset26);
        Assert.Equal(16, (restored with { Modal701 = 1, Modal702 = 0 }).AdvanceCommandRow4BB9E0().CommandRow!.Offset26);
        Assert.Equal(0, (restored with { Flags942 = 0x41 }).AdvanceCommandRow4BB9E0().CommandRow!.Offset26);
        Assert.Equal(8, (restored with { PauseDE9 = true }).AdvanceCommandRow4BB9E0().CommandRow!.Offset26);
        Assert.Throws<NotSupportedException>(() => (restored with { Modal701 = null }).AdvanceCommandRow4BB9E0());
        Assert.Throws<NotSupportedException>(() => (restored with { CommandRow = null }).AdvanceCommandRow4BB9E0());
    }

    [Fact]
    public void Original_command_callback_advances_clamps_reverses_and_confirms_partial_selection()
    {
        NativeBattleLimitCommandRow row = NativeBattleLimitCommandRow.Reset4BB840();
        for (int index = 0; index < 10; index++)
        {
            row = row.Advance4BB9E0(0x2000, 4);
            Assert.Equal(Math.Min((index + 1) * 8, 64), row.Offset26);
        }
        Assert.Equal(0, row.OrdinaryClipWidth4BCE10(100));
        row = row.Advance4BB9E0(0, 4);
        Assert.Equal(56, row.Offset26);
        Assert.True(row.SelectsLimit(4));
        Assert.Equal(64, row.Confirm4BC770(4).Offset26);
        Assert.Equal(0, row.Confirm4BC770(0).Offset26);
        for (int index = 0; index < 10; index++)
        {
            row = row.Advance4BB9E0(0, 4);
        }
        Assert.Equal(0, row.Offset26);
        Assert.False(row.SelectsLimit(4));
        Assert.Equal(100, row.OrdinaryClipWidth4BCE10(100));
    }

    [Theory]
    [InlineData(0x3000, 4)]
    [InlineData(0x6000, 4)]
    [InlineData(0x2000, 0)]
    public void Opposite_vertical_input_or_absent_row_flag_reverses_the_slide(int held, int flags)
    {
        Assert.Equal(8, new NativeBattleLimitCommandRow(16).Advance4BB9E0((ushort)held, (byte)flags).Offset26);
    }

    [Fact]
    public void Independently_restored_partial_row_uses_original_decay_and_next_callback()
    {
        NativeBattleLimitCommandRow original = new NativeBattleLimitCommandRow(0).Advance4BB9E0(0x2000, 4);
        NativeBattleLimitCommandRow restored = JsonSerializer.Deserialize<NativeBattleLimitCommandRow>(JsonSerializer.Serialize(original))!;
        //49ACD0: seven integer9/10 steps produce1957, then /64 gives30.
        Assert.Equal(46, restored.OrdinaryClipWidth4BCE10(100));
        Assert.Equal(original.Advance4BB9E0(0x2000, 4), restored.Advance4BB9E0(0x2000, 4));
        Assert.Equal(original.Advance4BB9E0(0, 4), restored.Advance4BB9E0(0, 4));
        Assert.Equal(original.Confirm4BC770(4), restored.Confirm4BC770(4));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleLimitCommandRow>("{}"));
        Assert.Throws<InvalidDataException>(() => new NativeBattleLimitCommandRow(65).Advance4BB9E0(0x2000, 4));
    }
}
