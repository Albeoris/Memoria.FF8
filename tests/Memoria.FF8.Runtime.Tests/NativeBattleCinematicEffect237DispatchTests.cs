namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect237DispatchTests
{
    [Theory]
    [InlineData("bone_table", 0x0, 0xA2A790u, 0xB262E0u)]
    [InlineData("bone_table", 0x1, 0xA2A790u, 0xB262E0u)]
    [InlineData("bone_table", 0x3, 0xA2A8A0u, 0xB263F0u)]
    [InlineData("bone_table", 0x4, 0xA2A950u, 0xA671C0u)]
    [InlineData("bone_table", 0x8, 0xA2D400u, 0xA2D400u)]
    [InlineData("draw_table", 0x1, 0xA2B130u, 0xB26FB0u)]
    [InlineData("draw_table", 0x2, 0xA2B1D0u, 0xB27050u)]
    [InlineData("draw_table", 0x4, 0xA2AFA0u, 0xB26E20u)]
    [InlineData("draw_table", 0x5, 0xA2D950u, 0xB298D0u)]
    [InlineData("draw_table", 0x6, 0xA2DFA0u, 0xB29F20u)]
    [InlineData("pop_table", 0x0, 0xA2E540u, 0xB2A4C0u)]
    [InlineData("pop_table", 0x1, 0xA2E570u, 0xB2A4F0u)]
    [InlineData("pop_table", 0x4, 0xA2E5F0u, 0xB2A570u)]
    [InlineData("pop_table", 0x5, 0xA2E6B0u, 0xB2A630u)]
    [InlineData("pop_table", 0x7, 0xA2E8A0u, 0xB2A820u)]
    [InlineData("pop_table", 0x9, 0xA2E7D0u, 0xB2A750u)]
    [InlineData("pop_table", 0xA, 0xA2E840u, 0xB2A7C0u)]
    [InlineData("pop_table", 0xB, 0xA2E910u, 0xB2A890u)]
    [InlineData("prim_table", 0x8, 0xA2C380u, 0xB28110u)]
    [InlineData("prim_table", 0x30, 0xA2C9D0u, 0xB11100u)]
    [InlineData("ptype_table", 0x1, 0xA2E360u, 0xB2A2E0u)]
    [InlineData("ptype_table", 0x2, 0xA2E3B0u, 0xB2A330u)]
    [InlineData("spr_table", 0x2, 0xA2D6D0u, 0xB29650u)]
    [InlineData("spr_table", 0x9, 0xA2D8B0u, 0xB29830u)]
    [InlineData("vm_table", 0x0, 0xA33E00u, 0xB2FEB0u)]
    [InlineData("vm_table", 0x1, 0xA33A70u, 0xB2FB20u)]
    [InlineData("vm_table", 0x2, 0xA2ED70u, 0xB2AE30u)]
    [InlineData("vm_table", 0x3, 0xA2ED90u, 0xB2AE50u)]
    [InlineData("vm_table", 0x4, 0xA2EDF0u, 0xB2AEB0u)]
    [InlineData("vm_table", 0x5, 0xA2FF70u, 0xB2C030u)]
    [InlineData("vm_table", 0x6, 0xA2F950u, 0xB2BA10u)]
    [InlineData("vm_table", 0x8, 0xA2FB10u, 0xB2BBD0u)]
    [InlineData("vm_table", 0x9, 0xA33DD0u, 0xB2FE80u)]
    [InlineData("vm_table", 0xA, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0xB, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0xC, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0xD, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0xE, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0x10, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0x11, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0x12, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0x13, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0x15, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0x16, 0xA303C0u, 0xB2C480u)]
    [InlineData("vm_table", 0x1A, 0xA30FB0u, 0xB2D070u)]
    [InlineData("vm_table", 0x1E, 0xA2EE40u, 0xB2AF00u)]
    [InlineData("vm_table", 0x1F, 0xA2EEC0u, 0xB2AF80u)]
    [InlineData("vm_table", 0x20, 0xA32230u, 0xB2E2F0u)]
    [InlineData("vm_table", 0x21, 0xA2F390u, 0xB2B450u)]
    [InlineData("vm_table", 0x22, 0xA2F470u, 0xB2B530u)]
    [InlineData("vm_table", 0x23, 0xA31EE0u, 0xB2DFA0u)]
    [InlineData("vm_table", 0x24, 0xA31A30u, 0xB2DAF0u)]
    [InlineData("vm_table", 0x25, 0xA319E0u, 0xB2DAA0u)]
    [InlineData("vm_table", 0x26, 0xA31400u, 0xB2D4C0u)]
    [InlineData("vm_table", 0x27, 0xA32480u, 0xB2E540u)]
    [InlineData("vm_table", 0x29, 0xA32380u, 0xB2E440u)]
    [InlineData("vm_table", 0x2B, 0xA2A060u, 0xB25BB0u)]
    [InlineData("vm_table", 0x2F, 0xA30810u, 0xB2C8D0u)]
    [InlineData("vm_table", 0x32, 0xA31550u, 0xB2D610u)]
    [InlineData("vm_table", 0x33, 0xA2EF60u, 0xB2B020u)]
    [InlineData("vm_table", 0x39, 0xA2F690u, 0xB2B750u)]
    [InlineData("vm_table", 0x3B, 0xA2F8C0u, 0xB2B980u)]
    [InlineData("vm_table", 0x3C, 0xA334E0u, 0xB2F3E0u)]
    [InlineData("vm_table", 0x3E, 0xA33630u, 0xB2F530u)]
    [InlineData("vm_table", 0x40, 0xA31C70u, 0xB2DD30u)]
    [InlineData("vm_table", 0x44, 0xA2F0F0u, 0xB2B1B0u)]
    [InlineData("vm_table", 0x4D, 0xA2AF70u, 0xB26DF0u)]
    [InlineData("vm_table", 0x52, 0xA32500u, 0xB2E5C0u)]
    [InlineData("vm_table", 0x58, 0xA2DDF0u, 0xB29D70u)]
    [InlineData("vm_table", 0x59, 0xA2DE60u, 0xB29DE0u)]
    [InlineData("vm_table", 0x5A, 0xA31810u, 0xB2D8D0u)]
    [InlineData("vm_table", 0x5D, 0xA31730u, 0xB2D7F0u)]
    [InlineData("vm_table", 0x60, 0xA30D40u, 0xB2CE00u)]
    [InlineData("vm_table", 0x61, 0xA30C00u, 0xB2CCC0u)]
    [InlineData("vm_table", 0x64, 0xA2F020u, 0xB2B0E0u)]
    [InlineData("vm_table", 0x67, 0xA32890u, 0xB2E950u)]
    [InlineData("vm_table", 0x68, 0xA32BF0u, 0xB2ECB0u)]
    [InlineData("vm_table", 0x6E, 0xA30EB0u, 0xB2CF70u)]
    [InlineData("vm_table", 0x73, 0xA31980u, 0xB2DA40u)]
    [InlineData("vm_table", 0x74, 0xA302A0u, 0xB2C360u)]
    [InlineData("vm_table", 0x7D, 0xA2FDD0u, 0xB2BE90u)]
    [InlineData("vm_table", 0x8B, 0xA31460u, 0xB2D520u)]
    [InlineData("vm_table", 0xA1, 0xA2EFC0u, 0xB2B080u)]
    [InlineData("vm_table", 0xA2, 0xA33F70u, 0xB30020u)]
    [InlineData("vm_table", 0xB0, 0xA33FB0u, 0xB30060u)]
    [InlineData("vm_table", 0xB1, 0xA2F090u, 0xB2B150u)]
    [InlineData("vm_table", 0xCE, 0xA2EF90u, 0xB2B050u)]
    [InlineData("vm_table", 0xD4, 0xA31D10u, 0xB2DDD0u)]
    [InlineData("vm_table", 0xF2, 0xA30B10u, 0xB2CBD0u)]
    [InlineData("vm_table", 0x112, 0xA32E10u, 0xA32E10u)]
    [InlineData("vm_table", 0x113, 0xA32F50u, 0xA32F50u)]
    [InlineData("vm_table", 0x116, 0xA32A50u, 0xB2EB10u)]
    public void Selected_handlers_reuse_their_current_kernel(string table, int index, uint selected, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(237, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(237, selected));
        if (selected != canonical)
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(237, table, index, canonical));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(247, table, index, selected));
    }

    [Theory]
    [InlineData("vm_table", 0x80, 0xA32E10u)]
    [InlineData("bone_table", 2, 0xA2D400u)]
    [InlineData("draw_table", 0x1B, 0xA2B1D0u)]
    [InlineData("prim_table", 0x44, 0xA2C9D0u)]
    [InlineData("ptype_table", 3, 0xA2E3B0u)]
    public void Unselected_slots_do_not_inherit_an_alias(string table, int index, uint selected)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(237, table, index, selected));
    }

    [Fact]
    public void Register5A_selected_orbit3_requires_its_current_handler()
    {
        Assert.Equal(0xB263F0u, NativeBattleCinematicDispatch.RequireSlot(237, "bone_table", 3, 0xA2A8A0));
        Assert.Equal(0xB263F0u, NativeBattleCinematicDispatch.Canonicalize(237, 0xA2A8A0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(237, "bone_table", 2, 0xA2A8A0));
    }

    [Fact]
    public void Selected_textured_triangle7_requires_its_current_handler()
    {
        Assert.Equal(0xB27E90u, NativeBattleCinematicDispatch.RequireSlot(237, "prim_table", 7, 0xA2C100));
        Assert.Equal(0xB27E90u, NativeBattleCinematicDispatch.Canonicalize(237, 0xA2C100));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(237, "prim_table", 6, 0xA2C100));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(237, "prim_table", 7, 0xB27E90));
    }

    [Fact]
    public void Register44_selected_culling31_requires_its_current_handler()
    {
        Assert.Equal(0xA2D000u, NativeBattleCinematicDispatch.RequireSlot(237, "draw_table", 31, 0xA2D000));
        Assert.Equal(0xA2D000u, NativeBattleCinematicDispatch.Canonicalize(237, 0xA2D000));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(237, "draw_table", 30, 0xA2D000));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(237, "draw_table", 32, 0xA2D000));
    }

    [Fact]
    public void Profile_preserves_selected_layout_and_loader()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(237);
        Assert.True(NativeBattleCinematicProfile.IsSupported(237));
        Assert.Equal(0x1865C9Cu, profile.BoneTable);
        Assert.Equal(0x1865CE4u, profile.PrimitiveTable);
        Assert.Equal(0x1865DE8u, profile.SpriteTable);
        Assert.Equal(0x1865E14u, profile.PlacementTable);
        Assert.Equal(0x1865E28u, profile.ParticleInstructionTable);
        Assert.Equal(0x1865E88u, profile.DrawTable);
        Assert.Equal(6, profile.TextureCount);
        Assert.Equal(0xA2FA80u, profile.LoadCallback);
        Assert.False(profile.Lit);
        Assert.False(profile.HasVibrationDirectory);
    }
}
