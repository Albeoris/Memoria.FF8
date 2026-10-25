namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect249DispatchTests
{
    [Theory]
    [MemberData(nameof(SelectedSlots))]
    public void Selected_slot_requires_its_actual_handler_and_current_individual_kernel(string table, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(249, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(249, table, index, handler ^ 1));
    }

    [Fact]
    public void Unselected_slots_reject_shared_handlers_and_unused_async_callback()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(249, "vm_table", 6, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(249, "vm_table", 0xB, 0x9BCC90));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(249, "draw_table", 6, 0x9BA220));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(249, "spr_table", 0, 0x9BA180));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(249, "prim_table", 48, 0x9B9710));
    }

    [Fact]
    public void Profile_retains_actual_publications_directories_and_unlit_selected_scope()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(249);
        Assert.True(NativeBattleCinematicProfile.IsSupported(249));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(0x185E51C));
        Assert.Equal(0x9B7380u, profile.Entry);
        Assert.Equal(0x9B7350u, profile.Initializer);
        Assert.Equal(0x27966A0u, profile.QueueAddress);
        Assert.Equal(0x27966B8u, profile.BankAddress);
        Assert.Equal(0x9B79F0u, profile.Callback);
        Assert.Equal(0u, profile.LoadCallback);
        Assert.Equal(0x27966B0u, profile.File0Publication);
        Assert.Equal(0x27966B4u, profile.File1Publication);
        Assert.Equal(0x185E9C4u, profile.InstructionTable);
        Assert.Equal(0x185E634u, profile.BoneTable);
        Assert.Equal(0x185E67Cu, profile.PrimitiveTable);
        Assert.Equal(0x185E780u, profile.SpriteTable);
        Assert.Equal(0x185E820u, profile.DrawTable);
        Assert.Equal(5, profile.TextureCount);
        Assert.Equal(0x9C4, profile.StaticLength);
        Assert.Equal(0x233, profile.SourceBase);
        Assert.Equal(0x232, profile.LastSource);
        Assert.Equal("mag248_b.00", profile.InitialFile);
        Assert.False(profile.Lit);
        Assert.False(profile.HasVibrationDirectory);
    }

    public static IEnumerable<object[]> SelectedSlots()
    {
        yield return ["vm_table", 0x0, 0x9C0490u, 0xB2FEB0u];
        yield return ["vm_table", 0x1, 0x9C0100u, 0xB2FB20u];
        yield return ["vm_table", 0x2, 0x9BB640u, 0xB2AE30u];
        yield return ["vm_table", 0x3, 0x9BB660u, 0xB2AE50u];
        yield return ["vm_table", 0x4, 0x9BB6C0u, 0xB2AEB0u];
        yield return ["vm_table", 0x5, 0x9BC840u, 0xB2C030u];
        yield return ["vm_table", 0x9, 0x9C0460u, 0xB2FE80u];
        yield return ["vm_table", 0xA, 0x9BCC90u, 0xB2C480u];
        yield return ["vm_table", 0xD, 0x9BCC90u, 0xB2C480u];
        yield return ["vm_table", 0xE, 0x9BCC90u, 0xB2C480u];
        yield return ["vm_table", 0x10, 0x9BCC90u, 0xB2C480u];
        yield return ["vm_table", 0x12, 0x9BCC90u, 0xB2C480u];
        yield return ["vm_table", 0x21, 0x9BBC60u, 0xB2B450u];
        yield return ["vm_table", 0x22, 0x9BBD40u, 0xB2B530u];
        yield return ["vm_table", 0x23, 0x9BE7B0u, 0xB2DFA0u];
        yield return ["vm_table", 0x24, 0x9BE300u, 0xB2DAF0u];
        yield return ["vm_table", 0x25, 0x9BE2B0u, 0xB2DAA0u];
        yield return ["vm_table", 0x26, 0x9BDCD0u, 0xB2D4C0u];
        yield return ["vm_table", 0x27, 0x9BED50u, 0xB2E540u];
        yield return ["vm_table", 0x2B, 0x9B77B0u, 0xB25BB0u];
        yield return ["vm_table", 0x32, 0x9BDE20u, 0xB2D610u];
        yield return ["vm_table", 0x33, 0x9BB830u, 0xB2B020u];
        yield return ["vm_table", 0x39, 0x9BBF60u, 0xB2B750u];
        yield return ["vm_table", 0x3B, 0x9BC190u, 0xB2B980u];
        yield return ["vm_table", 0x3C, 0x9BFBF0u, 0xB2F3E0u];
        yield return ["vm_table", 0x52, 0x9BEDD0u, 0xB2E5C0u];
        yield return ["vm_table", 0x5F, 0x9BCF90u, 0xB2C780u];
        yield return ["vm_table", 0x64, 0x9BB8F0u, 0xB2B0E0u];
        yield return ["vm_table", 0x68, 0x9BF4C0u, 0xB2ECB0u];
        yield return ["vm_table", 0x72, 0x9BD030u, 0xB2C820u];
        yield return ["vm_table", 0x74, 0x9BCB70u, 0xB2C360u];
        yield return ["vm_table", 0x7D, 0x9BC6A0u, 0xB2BE90u];
        yield return ["vm_table", 0x8B, 0x9BDD30u, 0xB2D520u];
        yield return ["vm_table", 0xB1, 0x9BB960u, 0xB2B150u];
        yield return ["vm_table", 0xD4, 0x9BE5E0u, 0xB2DDD0u];
        yield return ["vm_table", 0x104, 0x9BF5C0u, 0xB2EDB0u];
        yield return ["vm_table", 0x105, 0x9BF650u, 0xB2EE40u];
        yield return ["bone_table", 0x0, 0x9B7EE0u, 0xB262E0u];
        yield return ["bone_table", 0x1, 0x9B7EE0u, 0xB262E0u];
        yield return ["draw_table", 0x2, 0x9B87D0u, 0xB27050u];
        yield return ["draw_table", 0x5, 0x9BA220u, 0xB298D0u];
        yield return ["spr_table", 0x9, 0x9BA180u, 0xB29830u];
        yield return ["prim_table", 0x8, 0x9B9710u, 0xB28110u];
        yield return ["prim_table", 0x12, 0x9B9A40u, 0xB28DD0u];
    }
}
