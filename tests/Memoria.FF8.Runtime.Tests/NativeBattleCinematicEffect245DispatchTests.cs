namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect245DispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x0, 0x9E53D0u, 0xB2FEB0u)]
    [InlineData("vm_table", 0x1, 0x9E5040u, 0xB2FB20u)]
    [InlineData("vm_table", 0x2, 0x9E0580u, 0xB2AE30u)]
    [InlineData("vm_table", 0x3, 0x9E05A0u, 0xB2AE50u)]
    [InlineData("vm_table", 0x4, 0x9E0600u, 0xB2AEB0u)]
    [InlineData("vm_table", 0x5, 0x9E1780u, 0xB2C030u)]
    [InlineData("vm_table", 0x9, 0x9E53A0u, 0xB2FE80u)]
    [InlineData("vm_table", 0xA, 0x9E1BD0u, 0xB2C480u)]
    [InlineData("vm_table", 0xC, 0x9E1BD0u, 0xB2C480u)]
    [InlineData("vm_table", 0x10, 0x9E1BD0u, 0xB2C480u)]
    [InlineData("vm_table", 0x11, 0x9E1BD0u, 0xB2C480u)]
    [InlineData("vm_table", 0x12, 0x9E1BD0u, 0xB2C480u)]
    [InlineData("vm_table", 0x15, 0x9E1BD0u, 0xB2C480u)]
    [InlineData("vm_table", 0x1E, 0x9E0650u, 0xB2AF00u)]
    [InlineData("vm_table", 0x1F, 0x9E06D0u, 0xB2AF80u)]
    [InlineData("vm_table", 0x21, 0x9E0BA0u, 0xB2B450u)]
    [InlineData("vm_table", 0x23, 0x9E36F0u, 0xB2DFA0u)]
    [InlineData("vm_table", 0x24, 0x9E3240u, 0xB2DAF0u)]
    [InlineData("vm_table", 0x26, 0x9E2C10u, 0xB2D4C0u)]
    [InlineData("vm_table", 0x27, 0x9E3C90u, 0xB2E540u)]
    [InlineData("vm_table", 0x2B, 0x9DC9C0u, 0xB25BB0u)]
    [InlineData("vm_table", 0x32, 0x9E2D60u, 0xB2D610u)]
    [InlineData("vm_table", 0x39, 0x9E0EA0u, 0xB2B750u)]
    [InlineData("vm_table", 0x3B, 0x9E10D0u, 0xB2B980u)]
    [InlineData("vm_table", 0x4A, 0x9E4C10u, 0xB2F4C0u)]
    [InlineData("vm_table", 0x52, 0x9E3D10u, 0xB2E5C0u)]
    [InlineData("vm_table", 0x58, 0x9DF600u, 0xB29D70u)]
    [InlineData("vm_table", 0x59, 0x9DF670u, 0xB29DE0u)]
    [InlineData("vm_table", 0x60, 0x9E2550u, 0xB2CE00u)]
    [InlineData("vm_table", 0x64, 0x9E0830u, 0xB2B0E0u)]
    [InlineData("vm_table", 0x74, 0x9E1AB0u, 0xB2C360u)]
    [InlineData("vm_table", 0x7D, 0x9E15E0u, 0xB2BE90u)]
    [InlineData("vm_table", 0x8D, 0x9E16F0u, 0x9F3BF0u)]
    [InlineData("vm_table", 0xA1, 0x9E07D0u, 0xB2B080u)]
    [InlineData("vm_table", 0xB1, 0x9E08A0u, 0xB2B150u)]
    [InlineData("vm_table", 0xD4, 0x9E3520u, 0xB2DDD0u)]
    [InlineData("vm_table", 0xE7, 0x9E19A0u, 0xB2C250u)]
    [InlineData("bone_table", 0x0, 0x9DD0F0u, 0xB262E0u)]
    [InlineData("bone_table", 0x1, 0x9DD0F0u, 0xB262E0u)]
    [InlineData("draw_table", 0x6, 0x9DF7B0u, 0xB29F20u)]
    [InlineData("draw_table", 0x1B, 0x9DDD50u, 0xB272D0u)]
    [InlineData("prim_table", 0x8, 0x9DE920u, 0xB28110u)]
    [InlineData("pop_table", 0x0, 0x9DFD50u, 0xB2A4C0u)]
    [InlineData("pop_table", 0x1, 0x9DFD80u, 0xB2A4F0u)]
    [InlineData("pop_table", 0x2, 0x9DFDA0u, 0xB2A510u)]
    [InlineData("pop_table", 0x4, 0x9DFE00u, 0xB2A570u)]
    [InlineData("pop_table", 0x5, 0x9DFEC0u, 0xB2A630u)]
    [InlineData("pop_table", 0x7, 0x9E00B0u, 0xB2A820u)]
    [InlineData("pop_table", 0x10, 0x9E0240u, 0xB2A9B0u)]
    [InlineData("ptype_table", 0x1, 0x9DFB70u, 0xB2A2E0u)]
    public void Selected_handlers_reuse_their_current_kernel(string table, int index, uint selected, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(245, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(245, selected));
        if (selected != canonical)
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(245, table, index, canonical));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(270, table, index, selected));
    }

    [Theory]
    [InlineData("vm_table", 0x4D, 0x9E4C10u)]
    [InlineData("bone_table", 2, 0x9DD0F0u)]
    [InlineData("draw_table", 2, 0x9DDD50u)]
    [InlineData("prim_table", 0x30, 0x9DE920u)]
    public void Unselected_slots_do_not_inherit_an_alias(string table, int index, uint selected)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(245, table, index, selected));
    }

    [Fact]
    public void Profile_preserves_selected_layout_and_no_vibration_consumer()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(245);
        Assert.True(NativeBattleCinematicProfile.IsSupported(245));
        Assert.Equal(0x1860D54u, profile.BoneTable);
        Assert.Equal(4, profile.TextureCount);
        Assert.Equal(0u, profile.LoadCallback);
        Assert.False(profile.Lit);
        Assert.False(profile.HasVibrationDirectory);
    }
}
