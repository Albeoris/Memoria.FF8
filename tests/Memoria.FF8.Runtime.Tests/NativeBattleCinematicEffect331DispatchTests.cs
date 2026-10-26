namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect331DispatchTests
{
    [Theory]
    [InlineData("vm_table", 0xB5, 0x8E4B30u, 0x8E4B30u)]
    [InlineData("vm_table", 0xAD, 0x8E8DF0u, 0xA8EA50u)]
    [InlineData("vm_table", 0x39, 0x8E51E0u, 0xB2B750u)]
    [InlineData("draw_table", 0x26, 0x8E0FD0u, 0xAF59D0u)]
    [InlineData("draw_table", 0x1B, 0x8E1DC0u, 0xB272D0u)]
    [InlineData("prim_table", 8, 0x8E2990u, 0xB28110u)]
    [InlineData("prim_table", 9, 0x8E2C30u, 0xB283B0u)]
    [InlineData("pop_table", 0x10, 0x8E4560u, 0xB2A9B0u)]
    public void Selected_slots_require_their_current_native_handler(string table, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(331, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(331, table, index, handler + 1));
    }

    [Fact]
    public void Unselected_slot_is_not_inferred_from_a_known_handler()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(331, "draw_table", 1, 0x8E0FD0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(331, 0x8E0FD1));
    }

    [Fact]
    public void Profile_binds_both_actual_resources_and_selected_directories()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(331);
        Assert.Equal(NativeBattleEffect331Profile.Create(), profile);
        Assert.Equal(0x1852708u, profile.BoneTable);
        Assert.Equal(0x18528F4u, profile.DrawTable);
        Assert.Equal(0x1852894u, profile.ParticleInstructionTable);
        Assert.Equal(4, profile.TextureCount);
        Assert.True(profile.HasVibrationDirectory);
        Assert.False(profile.Lit);
        Assert.Equal(0u, profile.LoadCallback);
    }
}
