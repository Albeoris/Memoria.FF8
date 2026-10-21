using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageVitalsVisibilityTests
{
    [Fact]
    public void Initializer_sets_requested_rows_without_fabricating_a_draw_publication()
    {
        NativeBattleUiWindows windows = NativeBattleUiWindows.Initialize4A94D0();
        Assert.Equal((byte)7, windows.RequestedVitalsMask);
        Assert.Equal((byte)0, windows.CurrentVitalsMask);
        Assert.False(windows.IsVitalsRowVisible4B1740(0));
        windows = windows.RequestVitalsVisibility4B1830(0, false).RequestVitalsVisibility4B1830(2, false);
        Assert.Equal((byte)2, windows.RequestedVitalsMask);
        Assert.Equal((byte)0, windows.CurrentVitalsMask);
    }

    [Fact]
    public void Publication_copies_requested_rows_even_before_vitals_opening_completes()
    {
        NativeBattleUiWindows windows = NativeBattleUiWindows.Initialize4A94D0().RequestVitalsVisibility4B1830(0, false).RequestVitalsVisibility4B1830(2, false);
        NativeBattleUiWindows retained = windows.CommitDraw4B9DB0(false, 0x40);
        Assert.Equal((byte)0, retained.CurrentVitalsMask);
        NativeBattleUiWindows published = retained.CommitDraw4B9DB0(true, 0x40);
        Assert.Equal((byte)2, published.CurrentVitalsMask);
        Assert.False(published.DrawsVitals4B1740(0x40));
        Assert.True(published.IsVitalsRowVisible4B1740(1));
        Assert.False(published.IsVitalsRowVisible4B1740(0));
        Assert.False(published.IsVitalsRowVisible4B1740(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x41)]
    [InlineData(0x840)]
    public void Suppressed_vitals_callback_does_not_publish_visibility(ushort flags)
    {
        NativeBattleUiWindows windows = NativeBattleUiWindows.Initialize4A94D0();
        Assert.Equal((byte)0, windows.CommitDraw4B9DB0(true, flags).CurrentVitalsMask);
    }

    [Fact]
    public void Inactive_vitals_window_defers_publication_and_reveal_waits_for_actual_next_draw()
    {
        NativeBattleUiWindows windows = NativeBattleUiWindows.Initialize4A94D0().RequestVitalsVisibility4B1830(0, false);
        windows = windows with { Rows = windows.Rows.SetItem(7, windows.Rows[7] with { Current16 = 0, Requested17 = 0, Dirty18 = 0 }) };
        Assert.Equal((byte)0, windows.CommitDraw4B9DB0(true, 0x40).CurrentVitalsMask);
        windows = windows with { Rows = windows.Rows.SetItem(7, windows.Rows[7] with { Current16 = 2 }) };
        windows = windows.CommitDraw4B9DB0(true, 0x40);
        Assert.Equal((byte)6, windows.CurrentVitalsMask);
        windows = windows.RequestVitalsVisibility4B1830(0, true);
        Assert.Equal((byte)6, windows.CommitDraw4B9DB0(false, 0x40).CurrentVitalsMask);
        Assert.Equal((byte)7, windows.CommitDraw4B9DB0(true, 0x40).CurrentVitalsMask);
    }

    [Fact]
    public void JSON_restoration_preserves_pending_reveal_until_publication()
    {
        NativeBattleUiWindows windows = NativeBattleUiWindows.Initialize4A94D0().RequestVitalsVisibility4B1830(0, false).RequestVitalsVisibility4B1830(2, false).CommitDraw4B9DB0(true, 0x40);
        windows = windows.RequestVitalsVisibility4B1830(0, true).RequestVitalsVisibility4B1830(2, true);
        NativeBattleUiWindows restored = JsonSerializer.Deserialize<NativeBattleUiWindows>(JsonSerializer.Serialize(windows))!;
        Assert.Equal((byte)7, restored.RequestedVitalsMask);
        Assert.Equal((byte)2, restored.CurrentVitalsMask);
        Assert.Equal((byte)2, restored.CommitDraw4B9DB0(false, 0x40).CurrentVitalsMask);
        Assert.Equal((byte)7, restored.CommitDraw4B9DB0(true, 0x40).CurrentVitalsMask);
    }

    [Fact]
    public void Missing_snapshot_visibility_is_explicitly_unknown_and_rejected_at_its_consumer()
    {
        NativeBattleUiWindows windows = NativeBattleUiWindows.Initialize4A94D0() with { RequestedVitalsMask = null, CurrentVitalsMask = null };
        NativeBattleUiWindows restored = JsonSerializer.Deserialize<NativeBattleUiWindows>(JsonSerializer.Serialize(windows))!;
        Assert.Null(restored.RequestedVitalsMask);
        Assert.Null(restored.CurrentVitalsMask);
        Assert.Throws<NotSupportedException>(() => restored.RequestVitalsVisibility4B1830(0, false));
        Assert.Throws<NotSupportedException>(() => restored.IsVitalsRowVisible4B1740(0));
        Assert.Throws<NotSupportedException>(() => restored.CommitDraw4B9DB0(true, 0x40));
    }
}
