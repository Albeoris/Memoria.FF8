using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEffect240DispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x0, 0xA155F0u, 0xB2FEB0u)]
    [InlineData("vm_table", 0x1, 0xA15260u, 0xB2FB20u)]
    [InlineData("vm_table", 0x2, 0xA107A0u, 0xB2AE30u)]
    [InlineData("vm_table", 0x5, 0xA119A0u, 0xB2C030u)]
    [InlineData("vm_table", 0x9, 0xA155C0u, 0xB2FE80u)]
    [InlineData("vm_table", 0xA, 0xA11DF0u, 0xB2C480u)]
    [InlineData("vm_table", 0xD, 0xA11DF0u, 0xB2C480u)]
    [InlineData("vm_table", 0xE, 0xA11DF0u, 0xB2C480u)]
    [InlineData("vm_table", 0xF, 0xA11DF0u, 0xB2C480u)]
    [InlineData("vm_table", 0x10, 0xA11DF0u, 0xB2C480u)]
    [InlineData("vm_table", 0x11, 0xA11DF0u, 0xB2C480u)]
    [InlineData("vm_table", 0x12, 0xA11DF0u, 0xB2C480u)]
    [InlineData("vm_table", 0x15, 0xA11DF0u, 0xB2C480u)]
    [InlineData("vm_table", 0x1E, 0xA10870u, 0xB2AF00u)]
    [InlineData("vm_table", 0x1F, 0xA108F0u, 0xB2AF80u)]
    [InlineData("vm_table", 0x21, 0xA10DC0u, 0xB2B450u)]
    [InlineData("vm_table", 0x22, 0xA10EA0u, 0xB2B530u)]
    [InlineData("vm_table", 0x23, 0xA13910u, 0xB2DFA0u)]
    [InlineData("vm_table", 0x24, 0xA13460u, 0xB2DAF0u)]
    [InlineData("vm_table", 0x27, 0xA13EB0u, 0xB2E540u)]
    [InlineData("vm_table", 0x2B, 0xA0B790u, 0xB25BB0u)]
    [InlineData("vm_table", 0x2F, 0xA12240u, 0xB2C8D0u)]
    [InlineData("vm_table", 0x32, 0xA12F80u, 0xB2D610u)]
    [InlineData("vm_table", 0x33, 0xA10990u, 0xB2B020u)]
    [InlineData("vm_table", 0x39, 0xA110C0u, 0xB2B750u)]
    [InlineData("vm_table", 0x3B, 0xA112F0u, 0xB2B980u)]
    [InlineData("vm_table", 0x3C, 0xA14D50u, 0xB2F3E0u)]
    [InlineData("vm_table", 0x40, 0xA136A0u, 0xB2DD30u)]
    [InlineData("vm_table", 0x44, 0xA10B20u, 0xB2B1B0u)]
    [InlineData("vm_table", 0x47, 0xA11340u, 0xB2B9D0u)]
    [InlineData("vm_table", 0x4A, 0xA14E30u, 0xB2F4C0u)]
    [InlineData("vm_table", 0x52, 0xA13F30u, 0xB2E5C0u)]
    [InlineData("vm_table", 0x58, 0xA0F820u, 0xB29D70u)]
    [InlineData("vm_table", 0x59, 0xA0F890u, 0xB29DE0u)]
    [InlineData("vm_table", 0x5E, 0xA121E0u, 0xB2C870u)]
    [InlineData("vm_table", 0x64, 0xA10A50u, 0xB2B0E0u)]
    [InlineData("vm_table", 0x67, 0xA142C0u, 0xB2E950u)]
    [InlineData("vm_table", 0x68, 0xA14620u, 0xB2ECB0u)]
    [InlineData("vm_table", 0x6E, 0xA128E0u, 0xB2CF70u)]
    [InlineData("vm_table", 0x72, 0xA12190u, 0xB2C820u)]
    [InlineData("vm_table", 0x74, 0xA11CD0u, 0xB2C360u)]
    [InlineData("vm_table", 0x7D, 0xA11800u, 0xB2BE90u)]
    [InlineData("vm_table", 0x8B, 0xA12E90u, 0xB2D520u)]
    [InlineData("vm_table", 0xB1, 0xA10AC0u, 0xB2B150u)]
    [InlineData("pop_table", 0x0, 0xA0FF70u, 0xB2A4C0u)]
    [InlineData("pop_table", 0x1, 0xA0FFA0u, 0xB2A4F0u)]
    [InlineData("pop_table", 0x2, 0xA0FFC0u, 0xB2A510u)]
    [InlineData("pop_table", 0x4, 0xA10020u, 0xB2A570u)]
    [InlineData("pop_table", 0x5, 0xA100E0u, 0xB2A630u)]
    [InlineData("pop_table", 0x7, 0xA102D0u, 0xB2A820u)]
    [InlineData("pop_table", 0x9, 0xA10200u, 0xB2A750u)]
    [InlineData("pop_table", 0xA, 0xA10270u, 0xB2A7C0u)]
    [InlineData("pop_table", 0x10, 0xA10460u, 0xB2A9B0u)]
    [InlineData("bone_table", 0x0, 0xA0BEC0u, 0xB262E0u)]
    [InlineData("bone_table", 0x1, 0xA0BEC0u, 0xB262E0u)]
    [InlineData("draw_table", 0x2, 0xA0CB20u, 0xB27050u)]
    [InlineData("draw_table", 0x6, 0xA0F9D0u, 0xB29F20u)]
    [InlineData("draw_table", 0x1B, 0xA0CDA0u, 0xB272D0u)]
    [InlineData("prim_table", 0x8, 0xA0D970u, 0xB28110u)]
    [InlineData("prim_table", 0x9, 0xA0DC10u, 0xB283B0u)]
    [InlineData("prim_table", 0x12, 0xA0DF50u, 0xB28DD0u)]
    [InlineData("ptype_table", 0x1, 0xA0FD90u, 0xB2A2E0u)]
    [InlineData("ptype_table", 0x2, 0xA0FDE0u, 0xB2A330u)]
    public void Selected_handlers_reuse_their_current_kernel(string table, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(240, table, index, handler));
        Assert.Equal(canonical, NativeBattleCinematicDispatch.Canonicalize(240, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(240, table, index, canonical));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(239, table, index, handler));
    }

    [Fact]
    public void Clamped_mesh_requires_its_actual_draw_slot_and_preserves_its_identity()
    {
        Assert.Equal(0xA0E3D0u, NativeBattleCinematicDispatch.RequireSlot(240, "draw_table", 0x22, 0xA0E3D0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(240, 0xA0E3D0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(240, "draw_table", 2, 0xA0E3D0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(240, "draw_table", 0x22, 0xB27050));
    }

    [Theory]
    [InlineData("vm_table", 0xB, 0xA11DF0u)]
    [InlineData("bone_table", 2, 0xA0BEC0u)]
    [InlineData("pop_table", 8, 0xA102D0u)]
    [InlineData("ptype_table", 1, 0xA102D0u)]
    public void Unselected_slots_do_not_inherit_an_alias(string table, int index, uint handler)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(240, table, index, handler));
    }
}
