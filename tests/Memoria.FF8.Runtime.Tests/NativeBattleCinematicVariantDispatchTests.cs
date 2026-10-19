using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicVariantDispatchTests
{
    [Theory]
    [InlineData(205, 0xAFCBE0u)]
    [InlineData(206, 0xAF1830u)]
    public void Ring_variants_require_exact_selected_target_and_cannot_borrow_the_shared_return_stub(int effect, uint handler)
    {
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(effect, "vm_table", 0xCF, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, "vm_table", 0xCF, 0xB2D890));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(effect, handler));
        Assert.Equal(0xB2D890u, NativeBattleCinematicDispatch.RequireSlot(201, "vm_table", 0xCF, 0xB2D890));
    }
}
