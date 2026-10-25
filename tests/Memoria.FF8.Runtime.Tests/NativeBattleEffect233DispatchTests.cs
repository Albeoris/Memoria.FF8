namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect233DispatchTests
{
    [Theory]
    [InlineData("vm_table", 0x6F, 0xA5A6F0u)]
    [InlineData("vm_table", 0x70, 0xA53BA0u)]
    [InlineData("vm_table", 0x71, 0xA59E20u)]
    [InlineData("draw_table", 18, 0xA52E90u)]
    public void Selected_new_consumers_preserve_their_native_identity(string table, int index, uint handler)
    {
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(233, table, index, handler));
        Assert.Equal(handler, NativeBattleCinematicDispatch.Canonicalize(233, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(233, table, index + 1, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(233, table, index, handler + 1));
    }

    [Fact]
    public void Shared_vector_consumer_requires_an_admitted_slot_and_original_handler()
    {
        Assert.Equal(0xB2C480u, NativeBattleCinematicDispatch.RequireSlot(233, "vm_table", 0x15, 0xA585E0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(233, "vm_table", 0x14, 0xA585E0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(233, "spr_table", 0, 0xA55CE0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(233, 0));
    }

    [Fact]
    public void Profile_preserves_physical_tables_and_upload_count()
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(233);
        Assert.True(NativeBattleCinematicProfile.IsSupported(233));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(0x18682F8));
        Assert.Equal(0xA51F40u, profile.Entry);
        Assert.Equal(0xA51F10u, profile.Initializer);
        Assert.Equal(0x1868398u, profile.OwnerDirectoryAddress);
        Assert.Equal(0x2796A20u, profile.QueueAddress);
        Assert.Equal(0x2796A38u, profile.BankAddress);
        Assert.Equal(0xA525B0u, profile.Callback);
        Assert.Equal(0xA57CA0u, profile.LoadCallback);
        Assert.Equal(0x2796A34u, profile.File0Publication);
        Assert.Equal(0x2796A30u, profile.File1Publication);
        Assert.Equal(0x18683C0u, profile.BoneTable);
        Assert.Equal(0x1868408u, profile.PrimitiveTable);
        Assert.Equal(0x18684F8u, profile.PrimitiveSizes);
        Assert.Equal(0x186850Cu, profile.SpriteTable);
        Assert.Equal(0x1868538u, profile.PlacementTable);
        Assert.Equal(0x186854Cu, profile.ParticleInstructionTable);
        Assert.Equal(0x18685ACu, profile.DrawTable);
        Assert.Equal(0x1868750u, profile.InstructionTable);
        Assert.Equal(2, profile.TextureCount);
        Assert.Equal(0x233, profile.SourceBase);
        Assert.Equal(0x232, profile.LastSource);
        Assert.Equal("mag232_b.00", profile.InitialFile);
        Assert.False(profile.HasVibrationDirectory);
        Assert.False(profile.Lit);
    }
}
