using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuestCinematicTests
{
    [Theory]
    [InlineData(228, 0xA855C0u, 0xA85C30u, 0x2796B38u, 0x2796B50u, 6, 0x233)]
    [InlineData(261, 0x9598D0u, 0x959F40u, 0x2796158u, 0x27963F8u, 16, 0x2C8)]
    [InlineData(262, 0x94F9F0u, 0x950060u, 0x2795DE0u, 0x2796080u, 8, 0x233)]
    [InlineData(263, 0x945810u, 0x945E80u, 0x2795DA8u, 0x2795DB8u, 8, 0x233)]
    public void GuestProfilesRetainTheirActualPhysicalOwners(int effect, uint entry, uint callback, uint queue, uint bank, int textureCount, int sourceBase)
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        Assert.True(NativeBattleCinematicProfile.IsSupported(effect));
        Assert.Equal(entry, profile.Entry);
        Assert.Equal(callback, profile.Callback);
        Assert.Equal(queue, profile.QueueAddress);
        Assert.Equal(bank, profile.BankAddress);
        Assert.Equal(textureCount, profile.TextureCount);
        Assert.Equal(sourceBase, profile.SourceBase);
        Assert.False(profile.Lit);
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(profile.InitializationAddress));
        Assert.Equal(profile.InstructionTable + 0x147u * 4, profile.InitializationAddress + (uint)profile.StaticLength);
    }

    [Theory]
    [InlineData(228, "vm_table", 0x3F, 0xA86660u)]
    [InlineData(228, "vm_table", 0x41, 0xA86670u)]
    [InlineData(228, "vm_table", 0x89, 0xA86680u)]
    [InlineData(228, "vm_table", 0x93, 0xA8ED60u)]
    [InlineData(228, "draw_table", 3, 0xA866E0u)]
    [InlineData(261, "vm_table", 0x93, 0x963ED0u)]
    [InlineData(261, "draw_table", 21, 0x95A810u)]
    [InlineData(262, "vm_table", 0x3F, 0x950980u)]
    [InlineData(262, "vm_table", 0x41, 0x950990u)]
    [InlineData(262, "vm_table", 0x89, 0x9509A0u)]
    [InlineData(262, "vm_table", 0x93, 0x958DA0u)]
    [InlineData(262, "draw_table", 3, 0x950A00u)]
    [InlineData(262, "draw_table", 21, 0x950930u)]
    [InlineData(263, "vm_table", 0x3F, 0x9468B0u)]
    [InlineData(263, "vm_table", 0x41, 0x9468C0u)]
    [InlineData(263, "vm_table", 0x89, 0x9468D0u)]
    [InlineData(263, "vm_table", 0x93, 0x94EEC0u)]
    [InlineData(263, "draw_table", 3, 0x946930u)]
    public void OriginalRetStubsAreAdmittedOnlyAtTheirExactGuestSlot(int effect, string table, int index, uint handler)
    {
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(effect, table, index, handler));
        Assert.True(NativeBattleCinematicDispatch.IsNoOp(effect, table, index, handler));
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(effect, table, index, handler + 1));
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(201, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, table, index + 1, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(effect, handler));
    }

    [Theory]
    [InlineData(228, "vm_table", 0x39, 0xA8ADF0u)]
    [InlineData(228, "vm_table", 0xAD, 0xA8EA50u)]
    [InlineData(261, "vm_table", 0xAD, 0x963A90u)]
    [InlineData(261, "vm_table", 0x42, 0x963E30u)]
    [InlineData(228, "vm_table", 0xAB, 0xA86690u)]
    [InlineData(261, "vm_table", 0xAB, 0x95ABB0u)]
    [InlineData(262, "vm_table", 0xAB, 0x9509B0u)]
    [InlineData(263, "vm_table", 0xAB, 0x9468E0u)]
    [InlineData(261, "draw_table", 38, 0x95ACB0u)]
    [InlineData(262, "pop_table", 22, 0x9544B0u)]
    public void ImplementedGuestVariantsKeepTheirExactSelectedIdentity(int effect, string table, int index, uint handler)
    {
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(effect, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, table, index + 1, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(effect, handler));
    }

    [Theory]
    [InlineData(228, "prim_table", 12, 0xA88290u)]
    [InlineData(262, "prim_table", 9, 0x952620u)]
    public void UnimplementedGuestVariantsNeverUseAnApproximateSharedHandler(int effect, string table, int index, uint handler)
    {
        Assert.False(NativeBattleCinematicDispatch.IsNoOp(effect, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, table, index, handler));
    }
    [Theory]
    [InlineData(228, "draw_table", 5, 0xA890B0u, 0xB298D0u)]
    [InlineData(263, "vm_table", 0x6E, 0x94C8A0u, 0xB2CF70u)]
    [InlineData(261, "vm_table", 0x60, 0x961530u, 0xB2CE00u)]
    [InlineData(262, "vm_table", 0x60, 0x956610u, 0xB2CE00u)]
    [InlineData(263, "vm_table", 0x60, 0x94C730u, 0xB2CE00u)]
    public void ReachedAnchorAndRectangleClonesUseTheirAuthenticatedSharedKernels(int effect, string table, int index, uint handler, uint canonical)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(effect, table, index, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, table, index + 1, handler));
    }
}
