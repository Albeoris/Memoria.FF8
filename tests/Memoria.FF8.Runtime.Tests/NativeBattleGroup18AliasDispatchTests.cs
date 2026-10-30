namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup18AliasDispatchTests
{
    [Theory]
    [InlineData(229, 2, 0xA7D140u, 0xB27050u)]
    [InlineData(229, 6, 0xA7F480u, 0xB29F20u)]
    [InlineData(229, 37, 0xA7BBD0u, 0xAE8E20u)]
    [InlineData(230, 2, 0xA72110u, 0xB27050u)]
    [InlineData(230, 4, 0xA71EE0u, 0xB26E20u)]
    [InlineData(230, 5, 0xA742F0u, 0xB298D0u)]
    [InlineData(230, 21, 0xA71AB0u, 0xB266C0u)]
    [InlineData(256, 6, 0x97C5C0u, 0xB29F20u)]
    [InlineData(256, 26, 0x97A580u, 0xAEA0C0u)]
    [InlineData(257, 2, 0x9707F0u, 0xB27050u)]
    [InlineData(257, 4, 0x9705C0u, 0xB26E20u)]
    [InlineData(257, 5, 0x972D50u, 0xB298D0u)]
    [InlineData(257, 6, 0x9733A0u, 0xB29F20u)]
    [InlineData(257, 21, 0x9701D0u, 0xB266C0u)]
    [InlineData(257, 27, 0x970B60u, 0xB272D0u)]
    public void Selected_original_draw_rows_keep_their_exact_effect_selector_and_canonical_contract(int effect, int kind, uint original, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", kind, original));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", kind, original + 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, "pop_table", kind, original));
    }

    [Theory]
    [InlineData((ushort)229)]
    [InlineData((ushort)230)]
    [InlineData((ushort)256)]
    [InlineData((ushort)257)]
    public void Actual_slot_birth_and_resource_selection_are_retained_without_native_writes(ushort effect)
    {
        var frame = WaterSchedulerFixture.Create();
        var bank = new NativeBattleWaterSlotBank(0, 0x26258, 8, 0x2000);
        string native = NativeBattleTestSnapshots.Json(frame);
        var first = NativeBattleCinematicVisualIdentity.Identity(effect, frame, bank, 0);
        var repeated = NativeBattleCinematicVisualIdentity.Identity(effect, frame, bank, 0);
        Assert.Equal(first, repeated);
        Assert.Equal(effect, first.Effect);
        NativeBattleCinematicVisualIdentity.Allocated(frame.Arena, bank, 0);
        var recycled = NativeBattleCinematicVisualIdentity.Identity(effect, frame, bank, 0);
        Assert.NotEqual(first.Generation, recycled.Generation);
        Assert.Equal(first.Bank, recycled.Bank);
        Assert.Equal(first.Slot, recycled.Slot);
        Assert.Equal(native, NativeBattleTestSnapshots.Json(frame));
    }
}
