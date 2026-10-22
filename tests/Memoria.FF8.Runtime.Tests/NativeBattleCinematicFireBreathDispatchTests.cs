namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicFireBreathDispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x22, 0xA75E10u, 0xB2B530u, 0x23)]
    [InlineData("bone_table", 0, 0xA716D0u, 0xB262E0u, 0x11)]
    [InlineData("prim_table", 0x10, 0xA735D0u, 0xAEC030u, 0x11)]
    public void Exact_selected_handler_requires_its_physical_slot(string table, int index, uint selected, uint canonical, int wrongIndex)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(230, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(230, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(230, table, index, selected + 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(230, table, wrongIndex, selected));
    }

    [Theory]
    [InlineData("vm_table", 0x3F, 0xA71C10u)]
    [InlineData("vm_table", 0x41, 0xA71C20u)]
    [InlineData("vm_table", 0x89, 0xA71C30u)]
    [InlineData("vm_table", 0x93, 0xA79EF0u)]
    [InlineData("draw_table", 3, 0xA71C90u)]
    public void Literal_RET_preserves_physical_noop_identity(string table, int index, uint handler)
    {
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(230, table, index, handler));
        Assert.True(NativeBattleCinematicDispatch.IsNoOp(230, table, index, handler));
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(230, table, index, handler + 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(230, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(230, table, index, 0xA75E10));
    }

    [Theory]
    [InlineData(0x94, 0xA77C10u)]
    [InlineData(0xAB, 0xA71C40u)]
    public void Range_branch_and_actor_DAT_remain_distinct_selected_policies(int opcode, uint handler)
    {
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(230, "vm_table", opcode, handler));
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(230, "vm_table", opcode, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(230, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(230, "draw_table", opcode, handler));
    }

    [Fact]
    public void Noncallable_slot_rejects_null_and_foreign_callable_values()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(230, "vm_table", 0x18, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(230, "vm_table", 0x18, 0xA75E10));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(230, "vm_table", 0x18, 0xB2B530));
    }
}
