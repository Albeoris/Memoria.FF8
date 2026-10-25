using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect254DispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x0, 0x9978D0u, 0xB2FEB0u)]
    [InlineData("vm_table", 0x1, 0x997540u, 0xB2FB20u)]
    [InlineData("vm_table", 0x2, 0x9929A0u, 0xB2AE30u)]
    [InlineData("vm_table", 0x3, 0x9929C0u, 0xB2AE50u)]
    [InlineData("vm_table", 0x4, 0x992A20u, 0xB2AEB0u)]
    [InlineData("vm_table", 0x5, 0x993BA0u, 0xB2C030u)]
    [InlineData("vm_table", 0x9, 0x9978A0u, 0xB2FE80u)]
    [InlineData("vm_table", 0xA, 0x993FF0u, 0xB2C480u)]
    [InlineData("vm_table", 0xB, 0x993FF0u, 0xB2C480u)]
    [InlineData("vm_table", 0xC, 0x993FF0u, 0xB2C480u)]
    [InlineData("vm_table", 0xD, 0x993FF0u, 0xB2C480u)]
    [InlineData("vm_table", 0xE, 0x993FF0u, 0xB2C480u)]
    [InlineData("vm_table", 0x10, 0x993FF0u, 0xB2C480u)]
    [InlineData("vm_table", 0x11, 0x993FF0u, 0xB2C480u)]
    [InlineData("vm_table", 0x12, 0x993FF0u, 0xB2C480u)]
    [InlineData("vm_table", 0x20, 0x995E60u, 0xB2E2F0u)]
    [InlineData("vm_table", 0x21, 0x992FC0u, 0xB2B450u)]
    [InlineData("vm_table", 0x22, 0x9930A0u, 0xB2B530u)]
    [InlineData("vm_table", 0x23, 0x995B10u, 0xB2DFA0u)]
    [InlineData("vm_table", 0x24, 0x995660u, 0xB2DAF0u)]
    [InlineData("vm_table", 0x26, 0x995030u, 0xB2D4C0u)]
    [InlineData("vm_table", 0x27, 0x9960B0u, 0xB2E540u)]
    [InlineData("vm_table", 0x2A, 0x995100u, 0xB2D590u)]
    [InlineData("vm_table", 0x2B, 0x98D5E0u, 0xB25BB0u)]
    [InlineData("vm_table", 0x2F, 0x994440u, 0xB2C8D0u)]
    [InlineData("vm_table", 0x32, 0x995180u, 0xB2D610u)]
    [InlineData("vm_table", 0x33, 0x992B90u, 0xB2B020u)]
    [InlineData("vm_table", 0x39, 0x9932C0u, 0xB2B750u)]
    [InlineData("vm_table", 0x3B, 0x9934F0u, 0xB2B980u)]
    [InlineData("vm_table", 0x3C, 0x997030u, 0xB2F3E0u)]
    [InlineData("vm_table", 0x40, 0x9958A0u, 0xB2DD30u)]
    [InlineData("vm_table", 0x44, 0x992D20u, 0xB2B1B0u)]
    [InlineData("vm_table", 0x51, 0x9956B0u, 0xB2DB40u)]
    [InlineData("vm_table", 0x52, 0x996130u, 0xB2E5C0u)]
    [InlineData("vm_table", 0x58, 0x991A20u, 0xB29D70u)]
    [InlineData("vm_table", 0x59, 0x991A90u, 0xB29DE0u)]
    [InlineData("vm_table", 0x5E, 0x9943E0u, 0xB2C870u)]
    [InlineData("vm_table", 0x61, 0x994830u, 0xB2CCC0u)]
    [InlineData("vm_table", 0x62, 0x994830u, 0xB2CCC0u)]
    [InlineData("vm_table", 0x64, 0x992C50u, 0xB2B0E0u)]
    [InlineData("vm_table", 0x67, 0x9964C0u, 0xB2E950u)]
    [InlineData("vm_table", 0x68, 0x996820u, 0xB2ECB0u)]
    [InlineData("vm_table", 0x74, 0x993ED0u, 0xB2C360u)]
    [InlineData("vm_table", 0x7D, 0x993A00u, 0xB2BE90u)]
    [InlineData("vm_table", 0x8B, 0x995090u, 0xB2D520u)]
    [InlineData("vm_table", 0xAC, 0x994EB0u, 0xB2D340u)]
    [InlineData("vm_table", 0xB1, 0x992CC0u, 0xB2B150u)]
    [InlineData("vm_table", 0xD0, 0x995770u, 0xB2DC00u)]
    [InlineData("vm_table", 0xD3, 0x9959A0u, 0xB2DE30u)]
    [InlineData("vm_table", 0x10C, 0x993C70u, 0xB2C100u)]
    [InlineData("vm_table", 0x10D, 0x993D40u, 0xB2C1D0u)]
    [InlineData("vm_table", 0x116, 0x996680u, 0xB2EB10u)]
    [InlineData("bone_table", 0x0, 0x98DD10u, 0xB262E0u)]
    [InlineData("bone_table", 0x1, 0x98DD10u, 0xB262E0u)]
    [InlineData("draw_table", 0x2, 0x98F890u, 0xB27050u)]
    [InlineData("draw_table", 0x6, 0x991BD0u, 0xB29F20u)]
    [InlineData("draw_table", 0x25, 0x98E320u, 0xB1A130u)]
    [InlineData("prim_table", 0x7, 0x9907C0u, 0xB27E90u)]
    [InlineData("prim_table", 0x11, 0x990AD0u, 0xB28B00u)]
    [InlineData("prim_table", 0x12, 0x990DA0u, 0xB28DD0u)]
    [InlineData("pop_table", 0x0, 0x992170u, 0xB2A4C0u)]
    [InlineData("pop_table", 0x1, 0x9921A0u, 0xB2A4F0u)]
    [InlineData("pop_table", 0x4, 0x992220u, 0xB2A570u)]
    [InlineData("pop_table", 0x5, 0x9922E0u, 0xB2A630u)]
    [InlineData("pop_table", 0x7, 0x9924D0u, 0xB2A820u)]
    [InlineData("pop_table", 0x13, 0x992760u, 0xB2AAB0u)]
    [InlineData("ptype_table", 0x1, 0x991F90u, 0xB2A2E0u)]
    public void Selected_handlers_reuse_their_current_kernel(string table, int index, uint selected, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(254, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(254, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(254, table, index, canonical));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(253, table, index, selected));
    }

    [Fact]
    public void Actor_anchor_variant_requires_its_selected_instruction_slot()
    {
        Assert.Equal(0x996ED0u, NativeBattleCinematicDispatch.RequireSlot(254, "vm_table", 0xAD, 0x996ED0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(254, 0x996ED0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(254, "draw_table", 0xAD, 0x996ED0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(254, "vm_table", 0xAE, 0x996ED0));
    }

    [Theory]
    [InlineData("vm_table", 0xF, 0x993FF0u)]
    [InlineData("bone_table", 2, 0x98DD10u)]
    [InlineData("draw_table", 36, 0x98E320u)]
    [InlineData("pop_table", 8, 0x9924D0u)]
    public void Unselected_slots_do_not_inherit_an_alias(string table, int index, uint selected)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(254, table, index, selected));
    }
}
