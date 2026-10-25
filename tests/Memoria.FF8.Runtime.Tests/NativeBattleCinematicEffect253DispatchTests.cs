using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect253DispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x0, 0x9A2430u, 0xB2FEB0u)]
    [InlineData("vm_table", 0x1, 0x9A20A0u, 0xB2FB20u)]
    [InlineData("vm_table", 0x2, 0x99D500u, 0xB2AE30u)]
    [InlineData("vm_table", 0x3, 0x99D520u, 0xB2AE50u)]
    [InlineData("vm_table", 0x4, 0x99D580u, 0xB2AEB0u)]
    [InlineData("vm_table", 0x5, 0x99E700u, 0xB2C030u)]
    [InlineData("vm_table", 0x9, 0x9A2400u, 0xB2FE80u)]
    [InlineData("vm_table", 0xA, 0x99EB50u, 0xB2C480u)]
    [InlineData("vm_table", 0xB, 0x99EB50u, 0xB2C480u)]
    [InlineData("vm_table", 0xD, 0x99EB50u, 0xB2C480u)]
    [InlineData("vm_table", 0xE, 0x99EB50u, 0xB2C480u)]
    [InlineData("vm_table", 0x10, 0x99EB50u, 0xB2C480u)]
    [InlineData("vm_table", 0x11, 0x99EB50u, 0xB2C480u)]
    [InlineData("vm_table", 0x12, 0x99EB50u, 0xB2C480u)]
    [InlineData("vm_table", 0x20, 0x9A09C0u, 0xB2E2F0u)]
    [InlineData("vm_table", 0x21, 0x99DB20u, 0xB2B450u)]
    [InlineData("vm_table", 0x22, 0x99DC00u, 0xB2B530u)]
    [InlineData("vm_table", 0x23, 0x9A0670u, 0xB2DFA0u)]
    [InlineData("vm_table", 0x24, 0x9A01C0u, 0xB2DAF0u)]
    [InlineData("vm_table", 0x26, 0x99FB90u, 0xB2D4C0u)]
    [InlineData("vm_table", 0x27, 0x9A0C10u, 0xB2E540u)]
    [InlineData("vm_table", 0x2B, 0x998140u, 0xB25BB0u)]
    [InlineData("vm_table", 0x2F, 0x99EFA0u, 0xB2C8D0u)]
    [InlineData("vm_table", 0x32, 0x99FCE0u, 0xB2D610u)]
    [InlineData("vm_table", 0x33, 0x99D6F0u, 0xB2B020u)]
    [InlineData("vm_table", 0x39, 0x99DE20u, 0xB2B750u)]
    [InlineData("vm_table", 0x3B, 0x99E050u, 0xB2B980u)]
    [InlineData("vm_table", 0x3C, 0x9A1B90u, 0xB2F3E0u)]
    [InlineData("vm_table", 0x40, 0x9A0400u, 0xB2DD30u)]
    [InlineData("vm_table", 0x51, 0x9A0210u, 0xB2DB40u)]
    [InlineData("vm_table", 0x52, 0x9A0C90u, 0xB2E5C0u)]
    [InlineData("vm_table", 0x5E, 0x99EF40u, 0xB2C870u)]
    [InlineData("vm_table", 0x62, 0x99F390u, 0xB2CCC0u)]
    [InlineData("vm_table", 0x64, 0x99D7B0u, 0xB2B0E0u)]
    [InlineData("vm_table", 0x68, 0x9A1380u, 0xB2ECB0u)]
    [InlineData("vm_table", 0x74, 0x99EA30u, 0xB2C360u)]
    [InlineData("vm_table", 0x7D, 0x99E560u, 0xB2BE90u)]
    [InlineData("vm_table", 0x8B, 0x99FBF0u, 0xB2D520u)]
    [InlineData("vm_table", 0xAC, 0x99FA10u, 0xB2D340u)]
    [InlineData("vm_table", 0xB1, 0x99D820u, 0xB2B150u)]
    [InlineData("vm_table", 0xD3, 0x9A0500u, 0xB2DE30u)]
    [InlineData("vm_table", 0x10D, 0x99E8A0u, 0xB2C1D0u)]
    [InlineData("vm_table", 0x116, 0x9A11E0u, 0xB2EB10u)]
    [InlineData("vm_table", 0xC, 0x99EB50u, 0xB2C480u)]
    [InlineData("vm_table", 0x44, 0x99D880u, 0xB2B1B0u)]
    [InlineData("vm_table", 0x61, 0x99F390u, 0xB2CCC0u)]
    [InlineData("vm_table", 0xD0, 0x9A02D0u, 0xB2DC00u)]
    [InlineData("vm_table", 0x10C, 0x99E7D0u, 0xB2C100u)]
    [InlineData("vm_table", 0x2A, 0x99FC60u, 0xB2D590u)]
    [InlineData("vm_table", 0x58, 0x99C580u, 0xB29D70u)]
    [InlineData("vm_table", 0x59, 0x99C5F0u, 0xB29DE0u)]
    [InlineData("vm_table", 0x67, 0x9A1020u, 0xB2E950u)]
    [InlineData("bone_table", 0x0, 0x998870u, 0xB262E0u)]
    [InlineData("bone_table", 0x1, 0x998870u, 0xB262E0u)]
    [InlineData("draw_table", 0x2, 0x99A3F0u, 0xB27050u)]
    [InlineData("draw_table", 0x6, 0x99C730u, 0xB29F20u)]
    [InlineData("draw_table", 0x25, 0x998E80u, 0xB1A130u)]
    [InlineData("prim_table", 0x7, 0x99B320u, 0xB27E90u)]
    [InlineData("prim_table", 0x12, 0x99B900u, 0xB28DD0u)]
    [InlineData("pop_table", 0x13, 0x99D2C0u, 0xB2AAB0u)]
    [InlineData("prim_table", 0x11, 0x99B630u, 0xB28B00u)]
    [InlineData("pop_table", 0x0, 0x99CCD0u, 0xB2A4C0u)]
    [InlineData("pop_table", 0x1, 0x99CD00u, 0xB2A4F0u)]
    [InlineData("pop_table", 0x4, 0x99CD80u, 0xB2A570u)]
    [InlineData("pop_table", 0x5, 0x99CE40u, 0xB2A630u)]
    [InlineData("pop_table", 0x7, 0x99D030u, 0xB2A820u)]
    [InlineData("ptype_table", 0x1, 0x99CAF0u, 0xB2A2E0u)]
    public void Selected_handlers_reuse_their_current_kernel(string table, int index, uint selected, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(253, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(253, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(253, table, index, canonical));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(252, table, index, selected));
    }

    [Fact]
    public void Actor_anchor_variant_requires_its_selected_instruction_slot()
    {
        Assert.Equal(0x9A1A30u, NativeBattleCinematicDispatch.RequireSlot(253, "vm_table", 0xAD, 0x9A1A30));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(253, 0x9A1A30));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(253, "draw_table", 0xAD, 0x9A1A30));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(253, "vm_table", 0xAE, 0x9A1A30));
    }

    [Theory]
    [InlineData("vm_table", 0xF, 0x99EB50u)]
    [InlineData("bone_table", 2, 0x998870u)]
    [InlineData("draw_table", 36, 0x998E80u)]
    [InlineData("pop_table", 8, 0x99D030u)]
    public void Unselected_slots_do_not_inherit_an_alias(string table, int index, uint selected)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(253, table, index, selected));
    }
}
