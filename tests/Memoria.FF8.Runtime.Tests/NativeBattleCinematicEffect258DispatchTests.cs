using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect258DispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x0, 0x96EE50u, 0xB2FEB0u)]
    [InlineData("vm_table", 0x1, 0x96EAC0u, 0xB2FB20u)]
    [InlineData("vm_table", 0x2, 0x969EF0u, 0xB2AE30u)]
    [InlineData("vm_table", 0x3, 0x969F10u, 0xB2AE50u)]
    [InlineData("vm_table", 0x4, 0x969F70u, 0xB2AEB0u)]
    [InlineData("vm_table", 0x5, 0x96B0F0u, 0xB2C030u)]
    [InlineData("vm_table", 0x6, 0x96AAD0u, 0xB2BA10u)]
    [InlineData("vm_table", 0x8, 0x96AC90u, 0xB2BBD0u)]
    [InlineData("vm_table", 0x9, 0x96EE20u, 0xB2FE80u)]
    [InlineData("vm_table", 0xA, 0x96B540u, 0xB2C480u)]
    [InlineData("vm_table", 0xB, 0x96B540u, 0xB2C480u)]
    [InlineData("vm_table", 0xD, 0x96B540u, 0xB2C480u)]
    [InlineData("vm_table", 0x10, 0x96B540u, 0xB2C480u)]
    [InlineData("vm_table", 0x12, 0x96B540u, 0xB2C480u)]
    [InlineData("vm_table", 0x1E, 0x969FC0u, 0xB2AF00u)]
    [InlineData("vm_table", 0x1F, 0x96A040u, 0xB2AF80u)]
    [InlineData("vm_table", 0x21, 0x96A510u, 0xB2B450u)]
    [InlineData("vm_table", 0x22, 0x96A5F0u, 0xB2B530u)]
    [InlineData("vm_table", 0x23, 0x96D060u, 0xB2DFA0u)]
    [InlineData("vm_table", 0x26, 0x96C580u, 0xB2D4C0u)]
    [InlineData("vm_table", 0x27, 0x96D600u, 0xB2E540u)]
    [InlineData("vm_table", 0x29, 0x96D500u, 0xB2E440u)]
    [InlineData("vm_table", 0x2A, 0x96C650u, 0xB2D590u)]
    [InlineData("vm_table", 0x2B, 0x964E30u, 0xB25BB0u)]
    [InlineData("vm_table", 0x2F, 0x96B990u, 0xB2C8D0u)]
    [InlineData("vm_table", 0x32, 0x96C6D0u, 0xB2D610u)]
    [InlineData("vm_table", 0x39, 0x96A810u, 0xB2B750u)]
    [InlineData("vm_table", 0x3B, 0x96AA40u, 0xB2B980u)]
    [InlineData("vm_table", 0x40, 0x96CDF0u, 0xB2DD30u)]
    [InlineData("vm_table", 0x44, 0x96A270u, 0xB2B1B0u)]
    [InlineData("vm_table", 0x47, 0x96AA90u, 0xB2B9D0u)]
    [InlineData("vm_table", 0x52, 0x96D680u, 0xB2E5C0u)]
    [InlineData("vm_table", 0x58, 0x968F70u, 0xB29D70u)]
    [InlineData("vm_table", 0x59, 0x968FE0u, 0xB29DE0u)]
    [InlineData("vm_table", 0x64, 0x96A1A0u, 0xB2B0E0u)]
    [InlineData("vm_table", 0x74, 0x96B420u, 0xB2C360u)]
    [InlineData("vm_table", 0x7D, 0x96AF50u, 0xB2BE90u)]
    [InlineData("vm_table", 0x8B, 0x96C5E0u, 0xB2D520u)]
    [InlineData("vm_table", 0xB1, 0x96A210u, 0xB2B150u)]
    [InlineData("vm_table", 0xE, 0x96B540u, 0xB2C480u)]
    [InlineData("vm_table", 0x11, 0x96B540u, 0xB2C480u)]
    [InlineData("vm_table", 0x15, 0x96B540u, 0xB2C480u)]
    [InlineData("vm_table", 0x24, 0x96CBB0u, 0xB2DAF0u)]
    [InlineData("vm_table", 0x3C, 0x96E5B0u, 0xB2F3E0u)]
    [InlineData("vm_table", 0x4A, 0x96E690u, 0xB2F4C0u)]
    [InlineData("vm_table", 0x67, 0x96DA40u, 0xB2E950u)]
    [InlineData("vm_table", 0x68, 0x96DDA0u, 0xB2ECB0u)]
    [InlineData("vm_table", 0xD3, 0x96CEF0u, 0xB2DE30u)]
    [InlineData("vm_table", 0xD4, 0x96CE90u, 0xB2DDD0u)]
    [InlineData("bone_table", 0x0, 0x965560u, 0xB262E0u)]
    [InlineData("bone_table", 0x1, 0x965560u, 0xB262E0u)]
    [InlineData("draw_table", 0x2, 0x966BA0u, 0xB27050u)]
    [InlineData("draw_table", 0x6, 0x969120u, 0xB29F20u)]
    [InlineData("draw_table", 0x1A, 0x965B80u, 0xAEA0C0u)]
    [InlineData("draw_table", 0x1B, 0x966E20u, 0xB272D0u)]
    [InlineData("draw_table", 0x2B, 0x9660C0u, 0xB1B9E0u)]
    [InlineData("draw_table", 0x35, 0x9681E0u, 0xAED040u)]
    [InlineData("prim_table", 0x11, 0x967CF0u, 0xB28B00u)]
    [InlineData("prim_table", 0x7, 0x9679E0u, 0xB27E90u)]
    [InlineData("pop_table", 0x0, 0x9696C0u, 0xB2A4C0u)]
    [InlineData("pop_table", 0x1, 0x9696F0u, 0xB2A4F0u)]
    [InlineData("pop_table", 0x2, 0x969710u, 0xB2A510u)]
    [InlineData("pop_table", 0x4, 0x969770u, 0xB2A570u)]
    [InlineData("pop_table", 0x5, 0x969830u, 0xB2A630u)]
    [InlineData("pop_table", 0x7, 0x969A20u, 0xB2A820u)]
    [InlineData("pop_table", 0xB, 0x969A90u, 0xB2A890u)]
    [InlineData("pop_table", 0x10, 0x969BB0u, 0xB2A9B0u)]
    [InlineData("ptype_table", 0x1, 0x9694E0u, 0xB2A2E0u)]
    public void Selected_handlers_reuse_their_current_kernel(string table, int index, uint selected, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(258, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(258, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(258, table, index, canonical));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(257, table, index, selected));
    }

    [Fact]
    public void Actor_anchor_variant_requires_its_selected_instruction_slot()
    {
        Assert.Equal(0x96E450u, NativeBattleCinematicDispatch.RequireSlot(258, "vm_table", 0xAD, 0x96E450));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(258, 0x96E450));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(258, "draw_table", 0xAD, 0x96E450));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(258, "vm_table", 0xAE, 0x96E450));
    }

    [Theory]
    [InlineData("vm_table", 0xC, 0x96B540u)]
    [InlineData("bone_table", 2, 0x965560u)]
    [InlineData("draw_table", 1, 0x966BA0u)]
    [InlineData("pop_table", 7, 0x969120u)]
    public void Unselected_slots_do_not_inherit_an_alias(string table, int index, uint selected)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(258, table, index, selected));
    }
}
