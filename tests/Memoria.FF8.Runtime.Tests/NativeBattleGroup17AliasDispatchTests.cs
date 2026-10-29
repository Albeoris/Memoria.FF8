namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup17AliasDispatchTests
{
    [Theory]
    [InlineData("pop_table", 22, 0xAB8070u)]
    [InlineData("draw_table", 3, 0xAB4400u)]
    [InlineData("draw_table", 8, 0xAB4230u)]
    [InlineData("draw_table", 10, 0xAB7200u)]
    [InlineData("draw_table", 15, 0xAB47D0u)]
    [InlineData("draw_table", 16, 0xAB4240u)]
    [InlineData("draw_table", 17, 0xAB4250u)]
    [InlineData("draw_table", 18, 0xAB4380u)]
    [InlineData("draw_table", 19, 0xAB4570u)]
    [InlineData("draw_table", 20, 0xAB4430u)]
    [InlineData("draw_table", 22, 0xAB44C0u)]
    [InlineData("draw_table", 23, 0xAB4580u)]
    [InlineData("draw_table", 24, 0xAB4590u)]
    [InlineData("draw_table", 25, 0xAB4390u)]
    [InlineData("draw_table", 26, 0xAB45B0u)]
    [InlineData("draw_table", 28, 0xAB4610u)]
    [InlineData("draw_table", 29, 0xAB43A0u)]
    [InlineData("draw_table", 30, 0xAB4440u)]
    [InlineData("draw_table", 31, 0xAB69F0u)]
    [InlineData("draw_table", 32, 0xAB4450u)]
    [InlineData("draw_table", 33, 0xAB6A90u)]
    [InlineData("draw_table", 34, 0xAB6A10u)]
    [InlineData("draw_table", 35, 0xAB45C0u)]
    [InlineData("draw_table", 36, 0xAB45D0u)]
    [InlineData("draw_table", 37, 0xAB45A0u)]
    [InlineData("draw_table", 38, 0xAB4460u)]
    [InlineData("draw_table", 39, 0xAB6A20u)]
    [InlineData("draw_table", 40, 0xAB6A30u)]
    [InlineData("draw_table", 41, 0xAB47A0u)]
    [InlineData("draw_table", 42, 0xAB47B0u)]
    [InlineData("draw_table", 43, 0xAB47C0u)]
    [InlineData("draw_table", 44, 0xAB4410u)]
    [InlineData("draw_table", 45, 0xAB6A70u)]
    [InlineData("draw_table", 46, 0xAB71F0u)]
    [InlineData("draw_table", 47, 0xAB4600u)]
    [InlineData("draw_table", 48, 0xAB4470u)]
    [InlineData("draw_table", 49, 0xAB6AA0u)]
    [InlineData("draw_table", 50, 0xAB6AB0u)]
    [InlineData("draw_table", 51, 0xAB6A40u)]
    [InlineData("draw_table", 52, 0xAB4560u)]
    [InlineData("draw_table", 53, 0xAB6A80u)]
    [InlineData("draw_table", 54, 0xAB45E0u)]
    [InlineData("draw_table", 55, 0xAB44D0u)]
    [InlineData("draw_table", 56, 0xAB44E0u)]
    [InlineData("draw_table", 57, 0xAB4D50u)]
    [InlineData("draw_table", 58, 0xAB45F0u)]
    [InlineData("draw_table", 59, 0xAB44F0u)]
    [InlineData("draw_table", 60, 0xAB4500u)]
    [InlineData("draw_table", 61, 0xAB4510u)]
    [InlineData("draw_table", 62, 0xAB4520u)]
    [InlineData("draw_table", 63, 0xAB4530u)]
    [InlineData("draw_table", 64, 0xAB4540u)]
    [InlineData("draw_table", 65, 0xAB4480u)]
    [InlineData("draw_table", 66, 0xAB6AC0u)]
    [InlineData("draw_table", 67, 0xAB6A60u)]
    [InlineData("draw_table", 68, 0xAB6A50u)]
    [InlineData("draw_table", 69, 0xAB44A0u)]
    [InlineData("draw_table", 70, 0xAB44B0u)]
    [InlineData("draw_table", 71, 0xAB4550u)]
    [InlineData("draw_table", 72, 0xAB4490u)]
    public void Light_pillar_preserves_all_original_declared_noop_rows(string table, int index, uint handler)
    {
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(220, table, index, handler));
        Assert.True(NativeBattleCinematicDispatch.IsNoOp(220, table, index, handler));
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(220, table, index, handler + 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(220, table, index, handler + 1));
    }

    [Theory]
    [InlineData(220, 7, 0xAB4B40u, 0xB27220u)]
    [InlineData(231, 2, 0xA68810u, 0xB27050u)]
    [InlineData(231, 6, 0xA6AB10u, 0xB29F20u)]
    [InlineData(232, 2, 0xA5DE00u, 0xB27050u)]
    [InlineData(232, 6, 0xA603C0u, 0xB29F20u)]
    [InlineData(232, 44, 0xA5D730u, 0xA5D730u)]
    [InlineData(238, 2, 0xA21320u, 0xB27050u)]
    [InlineData(238, 4, 0xA210F0u, 0xB26E20u)]
    [InlineData(238, 5, 0xA23500u, 0xB298D0u)]
    [InlineData(238, 6, 0xA23B50u, 0xB29F20u)]
    [InlineData(238, 21, 0xA20D00u, 0xB266C0u)]
    [InlineData(240, 2, 0xA0CB20u, 0xB27050u)]
    [InlineData(240, 6, 0xA0F9D0u, 0xB29F20u)]
    [InlineData(240, 27, 0xA0CDA0u, 0xB272D0u)]
    [InlineData(240, 34, 0xA0E3D0u, 0xA0E3D0u)]
    public void Genuine_draw_alias_keeps_exact_selected_domain_and_is_not_a_noop(int effect, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", index, handler));
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(effect, "draw_table", index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", index, handler + 1));
    }

    [Fact]
    public void Earthquake_uses_direct_particle_dispatch_and_keeps_its_original_placement_table()
    {
        var profile = NativeBattleCinematicProfile.ForEffect(240);
        Assert.Throws<NotSupportedException>(() => profile.SpriteTable);
        Assert.Equal(0x1864010u, profile.PlacementTable);
        Assert.Equal(0xB29F20u, NativeBattleCinematicDispatch.RequireSlot(240, "draw_table", 6, 0xA0F9D0));
    }
}
