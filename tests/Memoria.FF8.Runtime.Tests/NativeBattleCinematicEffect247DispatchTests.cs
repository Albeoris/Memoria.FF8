namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect247DispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x0, 0x9D2ED0u, 0xB2FEB0u)]
    [InlineData("vm_table", 0x1, 0x9D2B40u, 0xB2FB20u)]
    [InlineData("vm_table", 0x2, 0x9CE080u, 0xB2AE30u)]
    [InlineData("vm_table", 0x3, 0x9CE0A0u, 0xB2AE50u)]
    [InlineData("vm_table", 0x4, 0x9CE100u, 0xB2AEB0u)]
    [InlineData("vm_table", 0x5, 0x9CF280u, 0xB2C030u)]
    [InlineData("vm_table", 0x6, 0x9CEC60u, 0xB2BA10u)]
    [InlineData("vm_table", 0x8, 0x9CEE20u, 0xB2BBD0u)]
    [InlineData("vm_table", 0x9, 0x9D2EA0u, 0xB2FE80u)]
    [InlineData("vm_table", 0xA, 0x9CF6D0u, 0xB2C480u)]
    [InlineData("vm_table", 0xC, 0x9CF6D0u, 0xB2C480u)]
    [InlineData("vm_table", 0xD, 0x9CF6D0u, 0xB2C480u)]
    [InlineData("vm_table", 0xE, 0x9CF6D0u, 0xB2C480u)]
    [InlineData("vm_table", 0x10, 0x9CF6D0u, 0xB2C480u)]
    [InlineData("vm_table", 0x11, 0x9CF6D0u, 0xB2C480u)]
    [InlineData("vm_table", 0x12, 0x9CF6D0u, 0xB2C480u)]
    [InlineData("vm_table", 0x15, 0x9CF6D0u, 0xB2C480u)]
    [InlineData("vm_table", 0x16, 0x9CF6D0u, 0xB2C480u)]
    [InlineData("vm_table", 0x1E, 0x9CE150u, 0xB2AF00u)]
    [InlineData("vm_table", 0x1F, 0x9CE1D0u, 0xB2AF80u)]
    [InlineData("vm_table", 0x21, 0x9CE6A0u, 0xB2B450u)]
    [InlineData("vm_table", 0x23, 0x9D11F0u, 0xB2DFA0u)]
    [InlineData("vm_table", 0x26, 0x9D0710u, 0xB2D4C0u)]
    [InlineData("vm_table", 0x27, 0x9D1790u, 0xB2E540u)]
    [InlineData("vm_table", 0x29, 0x9D1690u, 0xB2E440u)]
    [InlineData("vm_table", 0x2A, 0x9D07E0u, 0xB2D590u)]
    [InlineData("vm_table", 0x2B, 0x9C9CC0u, 0xB25BB0u)]
    [InlineData("vm_table", 0x2F, 0x9CFB20u, 0xB2C8D0u)]
    [InlineData("vm_table", 0x32, 0x9D0860u, 0xB2D610u)]
    [InlineData("vm_table", 0x33, 0x9CE270u, 0xB2B020u)]
    [InlineData("vm_table", 0x39, 0x9CE9A0u, 0xB2B750u)]
    [InlineData("vm_table", 0x3C, 0x9D2630u, 0xB2F3E0u)]
    [InlineData("vm_table", 0x40, 0x9D0F80u, 0xB2DD30u)]
    [InlineData("vm_table", 0x45, 0x9CEED0u, 0xB2BC80u)]
    [InlineData("vm_table", 0x48, 0x9CFC30u, 0xB2C9E0u)]
    [InlineData("vm_table", 0x4A, 0x9D2710u, 0xB2F4C0u)]
    [InlineData("vm_table", 0x4D, 0x9CAA80u, 0xB26DF0u)]
    [InlineData("vm_table", 0x52, 0x9D1810u, 0xB2E5C0u)]
    [InlineData("vm_table", 0x58, 0x9CD100u, 0xB29D70u)]
    [InlineData("vm_table", 0x59, 0x9CD170u, 0xB29DE0u)]
    [InlineData("vm_table", 0x5A, 0x9D0B20u, 0xB2D8D0u)]
    [InlineData("vm_table", 0x5E, 0x9CFAC0u, 0xB2C870u)]
    [InlineData("vm_table", 0x61, 0x9CFF10u, 0xB2CCC0u)]
    [InlineData("vm_table", 0x64, 0x9CE330u, 0xB2B0E0u)]
    [InlineData("vm_table", 0x67, 0x9D1BA0u, 0xB2E950u)]
    [InlineData("vm_table", 0x68, 0x9D1F00u, 0xB2ECB0u)]
    [InlineData("vm_table", 0x74, 0x9CF5B0u, 0xB2C360u)]
    [InlineData("vm_table", 0x7D, 0x9CF0E0u, 0xB2BE90u)]
    [InlineData("vm_table", 0x8B, 0x9D0770u, 0xB2D520u)]
    [InlineData("vm_table", 0xA1, 0x9CE2D0u, 0xB2B080u)]
    [InlineData("vm_table", 0xB1, 0x9CE3A0u, 0xB2B150u)]
    [InlineData("vm_table", 0xD4, 0x9D1020u, 0xB2DDD0u)]
    [InlineData("vm_table", 0xE7, 0x9CF4A0u, 0xB2C250u)]
    [InlineData("vm_table", 0x10C, 0x9CF350u, 0xB2C100u)]
    [InlineData("bone_table", 0x0, 0x9CA3F0u, 0xB262E0u)]
    [InlineData("bone_table", 0x1, 0x9CA3F0u, 0xB262E0u)]
    [InlineData("bone_table", 0x3, 0x9CA500u, 0xB263F0u)]
    [InlineData("draw_table", 0x2, 0x9CACE0u, 0xB27050u)]
    [InlineData("draw_table", 0x4, 0x9CAAB0u, 0xB26E20u)]
    [InlineData("draw_table", 0x6, 0x9CD2B0u, 0xB29F20u)]
    [InlineData("draw_table", 0x1B, 0x9CB050u, 0xB272D0u)]
    [InlineData("prim_table", 0x8, 0x9CBE90u, 0xB28110u)]
    [InlineData("prim_table", 0x12, 0x9CC480u, 0xB28DD0u)]
    [InlineData("pop_table", 0x0, 0x9CD850u, 0xB2A4C0u)]
    [InlineData("pop_table", 0x1, 0x9CD880u, 0xB2A4F0u)]
    [InlineData("pop_table", 0x2, 0x9CD8A0u, 0xB2A510u)]
    [InlineData("pop_table", 0x4, 0x9CD900u, 0xB2A570u)]
    [InlineData("pop_table", 0x5, 0x9CD9C0u, 0xB2A630u)]
    [InlineData("pop_table", 0x7, 0x9CDBB0u, 0xB2A820u)]
    [InlineData("pop_table", 0x10, 0x9CDD40u, 0xB2A9B0u)]
    [InlineData("pop_table", 0x13, 0x9CDE40u, 0xB2AAB0u)]
    [InlineData("ptype_table", 0x1, 0x9CD670u, 0xB2A2E0u)]
    public void Selected_handlers_reuse_their_current_kernel(string table, int index, uint selected, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(247, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(247, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(247, table, index, canonical));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(246, table, index, selected));
    }

    [Theory]
    [InlineData("vm_table", 0x8D, 0x9CF350u)]
    [InlineData("bone_table", 2, 0x9CA3F0u)]
    [InlineData("draw_table", 5, 0x9CAAB0u)]
    [InlineData("prim_table", 0x30, 0x9CBE90u)]
    public void Unselected_slots_do_not_inherit_an_alias(string table, int index, uint selected)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(247, table, index, selected));
    }

    [Fact]
    public void Native_null_dispatch_stays_an_explicit_failure()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(247, "vm_table", 0x80, 0));
    }

    [Fact]
    public void Profile_preserves_selected_layout_and_vibration_consumer()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(247);
        Assert.True(NativeBattleCinematicProfile.IsSupported(247));
        Assert.Equal(0x185F9CCu, profile.BoneTable);
        Assert.Equal(6, profile.TextureCount);
        Assert.Equal(0x9CED90u, profile.LoadCallback);
        Assert.False(profile.Lit);
        Assert.True(profile.HasVibrationDirectory);
    }
}
