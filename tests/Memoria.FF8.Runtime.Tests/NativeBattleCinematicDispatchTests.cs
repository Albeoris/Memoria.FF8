using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicDispatchTests
{
    [Theory]
    [InlineData(6, 0xB585A0u)]
    [InlineData(202, 0xB18EC0u)]
    public void Closed_volume_handlers_select_the_same_current_kernel(int effect, uint selected)
    {
        Assert.Equal(0xB25CA0u, NativeBattleCinematicDispatch.Canonicalize(effect, selected));
        Assert.Equal(0xB25CA0u, NativeBattleCinematicDispatch.Canonicalize(201, 0xB25CA0));
    }

    [Theory]
    [InlineData(6, 0xB5CAF0u)]
    [InlineData(202, 0xB24C50u)]
    [InlineData(203, 0xB0D0F0u)]
    [InlineData(205, 0xAF59D0u)]
    [InlineData(206, 0xAEF450u)]
    [InlineData(201, 0x1BAD00u)]
    public void A_native_difference_or_unknown_target_never_uses_a_shared_kernel(int effect, uint handler)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(effect, handler));
    }

    [Fact]
    public void Null_table_data_is_not_an_executable_alias()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(201, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(6, 0));
    }

    [Theory]
    [InlineData(201, 0xB26B80u)]
    [InlineData(6, 0xB5A530u)]
    [InlineData(202, 0xB19F40u)]
    [InlineData(203, 0xB0D4A0u)]
    [InlineData(204, 0xB010A0u)]
    [InlineData(206, 0xAE4200u)]
    public void Embedded_model_wrapper_requires_its_actual_table_slot(int effect, uint selected)
    {
        Assert.Equal(0xB26B80u, NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", 3, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", 2, selected));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, "vm_table", 3, selected));
    }

    [Fact]
    public void The_distinct_model_with_shadow_wrapper_requires_its_own_contract()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(205, "draw_table", 3, 0xAF5810));
    }
}
