using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicCoronaDispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x0, 0x9ABBB0u, 0xB2FEB0u)]
    [InlineData("vm_table", 0x1, 0x9AB820u, 0xB2FB20u)]
    [InlineData("vm_table", 0x2, 0x9A6D60u, 0xB2AE30u)]
    [InlineData("vm_table", 0x5, 0x9A7F60u, 0xB2C030u)]
    [InlineData("vm_table", 0x9, 0x9ABB80u, 0xB2FE80u)]
    [InlineData("vm_table", 0xA, 0x9A83B0u, 0xB2C480u)]
    [InlineData("vm_table", 0xB, 0x9A83B0u, 0xB2C480u)]
    [InlineData("vm_table", 0xD, 0x9A83B0u, 0xB2C480u)]
    [InlineData("vm_table", 0xE, 0x9A83B0u, 0xB2C480u)]
    [InlineData("vm_table", 0xF, 0x9A83B0u, 0xB2C480u)]
    [InlineData("vm_table", 0x11, 0x9A83B0u, 0xB2C480u)]
    [InlineData("vm_table", 0x12, 0x9A83B0u, 0xB2C480u)]
    [InlineData("vm_table", 0x15, 0x9A83B0u, 0xB2C480u)]
    [InlineData("vm_table", 0x16, 0x9A83B0u, 0xB2C480u)]
    [InlineData("vm_table", 0x1E, 0x9A6E30u, 0xB2AF00u)]
    [InlineData("vm_table", 0x1F, 0x9A6EB0u, 0xB2AF80u)]
    [InlineData("vm_table", 0x21, 0x9A7380u, 0xB2B450u)]
    [InlineData("vm_table", 0x22, 0x9A7460u, 0xB2B530u)]
    [InlineData("vm_table", 0x23, 0x9A9ED0u, 0xB2DFA0u)]
    [InlineData("vm_table", 0x24, 0x9A9A20u, 0xB2DAF0u)]
    [InlineData("vm_table", 0x26, 0x9A93F0u, 0xB2D4C0u)]
    [InlineData("vm_table", 0x27, 0x9AA470u, 0xB2E540u)]
    [InlineData("vm_table", 0x2B, 0x9A2CA0u, 0xB25BB0u)]
    [InlineData("vm_table", 0x2F, 0x9A8800u, 0xB2C8D0u)]
    [InlineData("vm_table", 0x32, 0x9A9540u, 0xB2D610u)]
    [InlineData("vm_table", 0x33, 0x9A6F50u, 0xB2B020u)]
    [InlineData("vm_table", 0x39, 0x9A7680u, 0xB2B750u)]
    [InlineData("vm_table", 0x3B, 0x9A78B0u, 0xB2B980u)]
    [InlineData("vm_table", 0x3C, 0x9AB310u, 0xB2F3E0u)]
    [InlineData("vm_table", 0x3E, 0x9AB460u, 0xB2F530u)]
    [InlineData("vm_table", 0x40, 0x9A9C60u, 0xB2DD30u)]
    [InlineData("vm_table", 0x4D, 0x9A3A60u, 0xB26DF0u)]
    [InlineData("vm_table", 0x51, 0x9A9A70u, 0xB2DB40u)]
    [InlineData("vm_table", 0x52, 0x9AA4F0u, 0xB2E5C0u)]
    [InlineData("vm_table", 0x58, 0x9A5DE0u, 0xB29D70u)]
    [InlineData("vm_table", 0x59, 0x9A5E50u, 0xB29DE0u)]
    [InlineData("vm_table", 0x5B, 0x9AAFC0u, 0xB2F090u)]
    [InlineData("vm_table", 0x64, 0x9A7010u, 0xB2B0E0u)]
    [InlineData("vm_table", 0x67, 0x9AA880u, 0xB2E950u)]
    [InlineData("vm_table", 0x68, 0x9AABE0u, 0xB2ECB0u)]
    [InlineData("vm_table", 0x72, 0x9A8750u, 0xB2C820u)]
    [InlineData("vm_table", 0x74, 0x9A8290u, 0xB2C360u)]
    [InlineData("vm_table", 0x7D, 0x9A7DC0u, 0xB2BE90u)]
    [InlineData("vm_table", 0x8B, 0x9A9450u, 0xB2D520u)]
    [InlineData("vm_table", 0xB1, 0x9A7080u, 0xB2B150u)]
    [InlineData("vm_table", 0xBC, 0x9A9B90u, 0xB2DC60u)]
    [InlineData("vm_table", 0xD3, 0x9A9D60u, 0xB2DE30u)]
    [InlineData("vm_table", 0x104, 0x9AACE0u, 0xB2EDB0u)]
    [InlineData("vm_table", 0x105, 0x9AAD70u, 0xB2EE40u)]
    [InlineData("vm_table", 0x116, 0x9AAA40u, 0xB2EB10u)]
    [InlineData("bone_table", 0x0, 0x9A33D0u, 0xB262E0u)]
    [InlineData("bone_table", 0x1, 0x9A33D0u, 0xB262E0u)]
    [InlineData("draw_table", 0x2, 0x9A3CC0u, 0xB27050u)]
    [InlineData("draw_table", 0x4, 0x9A3A90u, 0xB26E20u)]
    [InlineData("draw_table", 0x6, 0x9A5F90u, 0xB29F20u)]
    [InlineData("pop_table", 0x7, 0x9A6890u, 0xB2A820u)]
    [InlineData("pop_table", 0x0, 0x9A6530u, 0xB2A4C0u)]
    [InlineData("pop_table", 0x1, 0x9A6560u, 0xB2A4F0u)]
    [InlineData("pop_table", 0x4, 0x9A65E0u, 0xB2A570u)]
    [InlineData("pop_table", 0xB, 0x9A6900u, 0xB2A890u)]
    [InlineData("pop_table", 0xC, 0x9A6940u, 0xB2A8D0u)]
    [InlineData("pop_table", 0x10, 0x9A6A20u, 0xB2A9B0u)]
    [InlineData("pop_table", 0x2, 0x9A6580u, 0xB2A510u)]
    [InlineData("prim_table", 0x7, 0x9A4BF0u, 0xB27E90u)]
    [InlineData("prim_table", 0x11, 0x9A5160u, 0xB28B00u)]
    [InlineData("prim_table", 0x10, 0x9A4EF0u, 0xB105B0u)]
    [InlineData("ptype_table", 0x1, 0x9A6350u, 0xB2A2E0u)]
    public void Selected_handlers_reuse_their_current_kernel(string table, int index, uint selected, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(252, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(252, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(252, table, index, canonical));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(251, table, index, selected));
    }

    [Theory]
    [InlineData("vm_table", 0xC, 0x9A83B0u)]
    [InlineData("vm_table", 0x10, 0x9A83B0u)]
    [InlineData("bone_table", 2, 0x9A33D0u)]
    [InlineData("draw_table", 3, 0x9A3CC0u)]
    [InlineData("pop_table", 8, 0x9A6890u)]
    [InlineData("spr_table", 1, 0x9A6890u)]
    [InlineData("prim_table", 0x25, 0x9A5160u)]
    [InlineData("prim_table", 0x39, 0x9A5160u)]
    [InlineData("ptype_table", 0x2, 0x9A6350u)]
    public void Unselected_slots_do_not_inherit_an_alias(string table, int index, uint selected)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(252, table, index, selected));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x9A6590u)]
    [InlineData(0xB2A510u)]
    public void Unselected_targets_do_not_gain_a_contract(uint handler)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(252, handler));
    }
}
