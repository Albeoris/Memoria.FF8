namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeteor223Branch55DispatchTests
{
    [Fact]
    public void Selected_branch_resolves_to_the_executable_action_branch_kernel()
    {
        Assert.Equal(0xB2B380u, NativeBattleCinematicDispatch.RequireSlot(223, "vm_table", 0x55, 0xA96330));
        Assert.Equal(0xB2B380u, NativeBattleCinematicDispatch.Canonicalize(223, 0xA96330));
    }

    [Theory]
    [InlineData(0xA96331u)]
    [InlineData(0xB2B380u)]
    public void Branch_slot_rejects_a_handler_other_than_its_actual_selected_target(uint handler)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(223, "vm_table", 0x55, handler));
    }
}
