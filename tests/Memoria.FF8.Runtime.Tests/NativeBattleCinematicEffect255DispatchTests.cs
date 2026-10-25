using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect255DispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x0, 0x98CD70u, 0xB2FEB0u)]
    [InlineData("vm_table", 0x1, 0x98C9E0u, 0xB2FB20u)]
    [InlineData("vm_table", 0x2, 0x987E40u, 0xB2AE30u)]
    [InlineData("vm_table", 0x3, 0x987E60u, 0xB2AE50u)]
    [InlineData("vm_table", 0x4, 0x987EC0u, 0xB2AEB0u)]
    [InlineData("vm_table", 0x5, 0x989040u, 0xB2C030u)]
    [InlineData("vm_table", 0x9, 0x98CD40u, 0xB2FE80u)]
    [InlineData("vm_table", 0xA, 0x989490u, 0xB2C480u)]
    [InlineData("vm_table", 0xB, 0x989490u, 0xB2C480u)]
    [InlineData("vm_table", 0xC, 0x989490u, 0xB2C480u)]
    [InlineData("vm_table", 0xD, 0x989490u, 0xB2C480u)]
    [InlineData("vm_table", 0xE, 0x989490u, 0xB2C480u)]
    [InlineData("vm_table", 0x10, 0x989490u, 0xB2C480u)]
    [InlineData("vm_table", 0x11, 0x989490u, 0xB2C480u)]
    [InlineData("vm_table", 0x12, 0x989490u, 0xB2C480u)]
    [InlineData("vm_table", 0x20, 0x98B300u, 0xB2E2F0u)]
    [InlineData("vm_table", 0x21, 0x988460u, 0xB2B450u)]
    [InlineData("vm_table", 0x22, 0x988540u, 0xB2B530u)]
    [InlineData("vm_table", 0x23, 0x98AFB0u, 0xB2DFA0u)]
    [InlineData("vm_table", 0x24, 0x98AB00u, 0xB2DAF0u)]
    [InlineData("vm_table", 0x26, 0x98A4D0u, 0xB2D4C0u)]
    [InlineData("vm_table", 0x27, 0x98B550u, 0xB2E540u)]
    [InlineData("vm_table", 0x2A, 0x98A5A0u, 0xB2D590u)]
    [InlineData("vm_table", 0x2B, 0x982A80u, 0xB25BB0u)]
    [InlineData("vm_table", 0x2F, 0x9898E0u, 0xB2C8D0u)]
    [InlineData("vm_table", 0x32, 0x98A620u, 0xB2D610u)]
    [InlineData("vm_table", 0x33, 0x988030u, 0xB2B020u)]
    [InlineData("vm_table", 0x39, 0x988760u, 0xB2B750u)]
    [InlineData("vm_table", 0x3B, 0x988990u, 0xB2B980u)]
    [InlineData("vm_table", 0x3C, 0x98C4D0u, 0xB2F3E0u)]
    [InlineData("vm_table", 0x40, 0x98AD40u, 0xB2DD30u)]
    [InlineData("vm_table", 0x44, 0x9881C0u, 0xB2B1B0u)]
    [InlineData("vm_table", 0x51, 0x98AB50u, 0xB2DB40u)]
    [InlineData("vm_table", 0x52, 0x98B5D0u, 0xB2E5C0u)]
    [InlineData("vm_table", 0x58, 0x986EC0u, 0xB29D70u)]
    [InlineData("vm_table", 0x59, 0x986F30u, 0xB29DE0u)]
    [InlineData("vm_table", 0x5E, 0x989880u, 0xB2C870u)]
    [InlineData("vm_table", 0x61, 0x989CD0u, 0xB2CCC0u)]
    [InlineData("vm_table", 0x62, 0x989CD0u, 0xB2CCC0u)]
    [InlineData("vm_table", 0x64, 0x9880F0u, 0xB2B0E0u)]
    [InlineData("vm_table", 0x67, 0x98B960u, 0xB2E950u)]
    [InlineData("vm_table", 0x68, 0x98BCC0u, 0xB2ECB0u)]
    [InlineData("vm_table", 0x74, 0x989370u, 0xB2C360u)]
    [InlineData("vm_table", 0x7D, 0x988EA0u, 0xB2BE90u)]
    [InlineData("vm_table", 0x8B, 0x98A530u, 0xB2D520u)]
    [InlineData("vm_table", 0xAC, 0x98A350u, 0xB2D340u)]
    [InlineData("vm_table", 0xB1, 0x988160u, 0xB2B150u)]
    [InlineData("vm_table", 0xD0, 0x98AC10u, 0xB2DC00u)]
    [InlineData("vm_table", 0xD3, 0x98AE40u, 0xB2DE30u)]
    [InlineData("vm_table", 0x10C, 0x989110u, 0xB2C100u)]
    [InlineData("vm_table", 0x10D, 0x9891E0u, 0xB2C1D0u)]
    [InlineData("vm_table", 0x116, 0x98BB20u, 0xB2EB10u)]
    [InlineData("bone_table", 0x0, 0x9831B0u, 0xB262E0u)]
    [InlineData("bone_table", 0x1, 0x9831B0u, 0xB262E0u)]
    [InlineData("draw_table", 0x2, 0x984D30u, 0xB27050u)]
    [InlineData("draw_table", 0x6, 0x987070u, 0xB29F20u)]
    [InlineData("draw_table", 0x25, 0x9837C0u, 0xB1A130u)]
    [InlineData("prim_table", 0x7, 0x985C60u, 0xB27E90u)]
    [InlineData("prim_table", 0x11, 0x985F70u, 0xB28B00u)]
    [InlineData("prim_table", 0x12, 0x986240u, 0xB28DD0u)]
    [InlineData("pop_table", 0x0, 0x987610u, 0xB2A4C0u)]
    [InlineData("pop_table", 0x1, 0x987640u, 0xB2A4F0u)]
    [InlineData("pop_table", 0x4, 0x9876C0u, 0xB2A570u)]
    [InlineData("pop_table", 0x5, 0x987780u, 0xB2A630u)]
    [InlineData("pop_table", 0x7, 0x987970u, 0xB2A820u)]
    [InlineData("pop_table", 0x13, 0x987C00u, 0xB2AAB0u)]
    [InlineData("ptype_table", 0x1, 0x987430u, 0xB2A2E0u)]
    public void Selected_handlers_reuse_their_current_kernel(string table, int index, uint selected, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(255, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(255, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(255, table, index, canonical));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(253, table, index, selected));
    }

    [Fact]
    public void Actor_anchor_variant_requires_its_selected_instruction_slot()
    {
        Assert.Equal(0x98C370u, NativeBattleCinematicDispatch.RequireSlot(255, "vm_table", 0xAD, 0x98C370));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(255, 0x98C370));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(255, "draw_table", 0xAD, 0x98C370));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(255, "vm_table", 0xAE, 0x98C370));
    }

    [Theory]
    [InlineData("vm_table", 0xF, 0x989490u)]
    [InlineData("bone_table", 2, 0x9831B0u)]
    [InlineData("draw_table", 36, 0x9837C0u)]
    [InlineData("pop_table", 8, 0x987970u)]
    public void Unselected_slots_do_not_inherit_an_alias(string table, int index, uint selected)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(255, table, index, selected));
    }
}
