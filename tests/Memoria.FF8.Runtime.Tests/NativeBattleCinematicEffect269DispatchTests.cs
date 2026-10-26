namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect269DispatchTests
{
    [Theory]
    [InlineData("prim_table", 0x11, 0x8FD4A0u, 0xB28B00u)]
    [InlineData("prim_table", 0x10, 0x8FD230u, 0xB105B0u)]
    [InlineData("prim_table", 0x7, 0x8FC9F0u, 0xB27E90u)]
    [InlineData("draw_table", 0x2, 0x8FBCA0u, 0xB27050u)]
    [InlineData("vm_table", 0x35, 0x902F80u, 0xA996A0u)]
    [InlineData("vm_table", 0x6, 0x9002D0u, 0xB2BA10u)]
    [InlineData("vm_table", 0x37, 0x900CC0u, 0xB2C400u)]
    [InlineData("vm_table", 0x13D, 0x8FFE90u, 0xB2B5D0u)]
    [InlineData("vm_table", 0x53, 0x8F77B0u, 0xB4AAC0u)]
    [InlineData("vm_table", 0x5F, 0x901040u, 0xB2C780u)]
    [InlineData("draw_table", 0x16, 0x8F88D0u, 0xB4BBE0u)]
    [InlineData("bone_table", 0x3, 0x8F7E90u, 0xB263F0u)]
    [InlineData("ptype_table", 0x2, 0x8FECD0u, 0xB2A330u)]
    [InlineData("pop_table", 0xA, 0x8FF160u, 0xB2A7C0u)]
    public void Selected_slots_require_their_current_native_handler(string table, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(269, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(269, table, index, handler + 1));
    }

    [Fact]
    public void Unselected_slot_is_not_inferred_from_a_known_handler()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(269, "draw_table", 1, 0x8F88D0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(269, 0x8F88D1));
    }

    [Fact]
    public void Profile_binds_the_actual_asynchronous_completion_and_directories()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(269);
        Assert.Equal(NativeBattleEffect269Profile.Create(), profile);
        Assert.Equal(0x1853B78u, profile.BoneTable);
        Assert.Equal(0x1853D64u, profile.DrawTable);
        Assert.Equal(0x900400u, profile.LoadCallback);
        Assert.Equal(9, profile.TextureCount);
        Assert.True(profile.HasVibrationDirectory);
        Assert.False(profile.Lit);
    }
}
