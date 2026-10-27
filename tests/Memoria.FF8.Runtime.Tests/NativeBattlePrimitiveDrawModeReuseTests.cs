namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrimitiveDrawModeReuseTests
{
    [Fact]
    public void Draw_reset_only_copies_packets_whose_physical_read_owner_changes()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(1, new("packet", 0, 0), [0], 0, []);
        NativeBattleLogicalOrdering before = NativeBattleStorageSnapshots.Capture(ordering);
        NativeBattleLogicalOrdering reset = ordering.ResetAfterDraw45D080();
        Assert.False(before.Packets[0].RequiresPhysicalRead);
        Assert.Same(ordering, reset);
        Assert.True(reset.Packets[0].RequiresPhysicalRead);
        Assert.Same(reset, reset.ResetAfterDraw45D080());
        NativeBattleLogicalOrdering changedCursor = reset.UpdateMetadata(1, reset.TableBuffer);
        Assert.Same(reset.Packets[0], changedCursor.ResetAfterDraw45D080().Packets[0]);
        Assert.Equal(0, changedCursor.ProxyCursor);
    }

    [Fact]
    public void Repeated_native_page_and_window_commands_preserve_all_retained_metadata_without_an_extra_state_copy()
    {
        NativeBattlePrimitiveDrawMode mode = new(0xB7) { TextureWindowBits = 0x80210, TexturePixelSnapshot = 7 };
        Assert.Same(mode, mode.ExecuteWords(0xE1, [0xE10000B7]));
        Assert.Same(mode, mode.ExecuteWords(0xE2, [0xE2080210]));
        Assert.Same(mode, mode.ExecuteWords(0x24, [0, 0, 0, 0, 0xB70000]));
        Assert.Same(mode, mode.ExecuteWords(0x34, [0, 0, 0, 0, 0, 0xB70000]));
        Assert.Equal((ushort?)0xB6, mode.ExecuteWords(0xE1, [0xE10000B6]).TexturePage);
        Assert.Equal(0u, mode.ExecuteWords(0xE2, [0xE2000000]).TextureWindowBits);
        Assert.Equal((int?)7, mode.ExecuteWords(0xE1, [0xE10000B6]).TexturePixelSnapshot);
    }
}
