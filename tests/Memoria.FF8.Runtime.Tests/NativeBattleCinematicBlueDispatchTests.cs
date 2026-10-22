namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBlueDispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x22, 0xA80950u, 0xB2B530u, 0x23)]
    [InlineData("bone_table", 0, 0xA7B580u, 0xB262E0u, 0x11)]
    [InlineData("prim_table", 7, 0xA7E070u, 0xB27E90u, 8)]
    [InlineData("draw_table", 0x25, 0xA7BBD0u, 0xAE8E20u, 0x26)]
    public void Selected_kernels_require_the_actual_physical_slot(string table, int index, uint selected, uint canonical, int wrongIndex)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(229, table, index, selected));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(229, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(229, table, index, selected + 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(229, table, wrongIndex, selected));
    }

    [Theory]
    [InlineData("vm_table", 0x3F, 0xA7B9B0u)]
    [InlineData("vm_table", 0x41, 0xA7B9C0u)]
    [InlineData("vm_table", 0x89, 0xA7B9D0u)]
    [InlineData("vm_table", 0x93, 0xA84A90u)]
    [InlineData("prim_table", 8, 0xA7E2F0u)]
    [InlineData("prim_table", 9, 0xA7E300u)]
    [InlineData("prim_table", 0xC, 0xA7E330u)]
    [InlineData("prim_table", 0x13, 0xA7E930u)]
    [InlineData("draw_table", 3, 0xA7BA30u)]
    [InlineData("draw_table", 0x15, 0xA7B960u)]
    public void Native_RET_is_an_exact_noop_identity_and_never_a_non_RET_alias(string table, int index, uint handler)
    {
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(229, table, index, handler));
        Assert.True(NativeBattleCinematicDispatch.IsNoOp(229, table, index, handler));
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(229, table, index, handler + 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(229, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(229, table, index, 0xA80950));
    }

    [Theory]
    [InlineData(0xAB, 0xA7B9E0u)]
    [InlineData(0xAD, 0xA84780u)]
    public void Actor_resource_and_pose_variants_keep_their_selected_physical_identity(int opcode, uint handler)
    {
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(229, "vm_table", opcode, handler));
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(229, "vm_table", opcode, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(229, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(229, "draw_table", opcode, handler));
    }

    [Fact]
    public void Native_noncallable_slot_rejects_even_an_otherwise_valid_kernel()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(229, "vm_table", 0x18, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(229, "vm_table", 0x18, 0xA80950));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(229, "vm_table", 0x18, 0xB2B530));
    }
}
