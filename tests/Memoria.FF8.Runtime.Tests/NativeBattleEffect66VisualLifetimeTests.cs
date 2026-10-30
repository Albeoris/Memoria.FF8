namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66VisualLifetimeTests
{
    [Fact]
    public void Fixed_and_allocated_point_banks_keep_birth_epochs_independent_of_temporary_contexts()
    {
        NativeBattleEffect66Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, new(66, 65, new(66, NativeBattleEffect66Profile.Entry, NativeBattleEffect66Profile.Initializer), null, null), new(123), NativeBattleEffect66State.FromColdProcess());
        uint fixedAddress = NativeBattleEffect66Profile.FirstPointAddress;
        uint allocatedAddress = 0x2100000;
        NativeBattleEffect66VisualIdentity.Allocated(frame, fixedAddress);
        NativeBattleEffect66VisualIdentity.Allocated(frame, allocatedAddress);
        NativeBattlePilotVisualIdentity a = NativeBattleEffect66VisualIdentity.Point(frame, fixedAddress, 0xDE90CC);
        NativeBattlePilotVisualIdentity b = NativeBattleEffect66VisualIdentity.Point(frame, allocatedAddress, 0xDE477C);
        Assert.Equal(a, NativeBattleEffect66VisualIdentity.Point(frame, fixedAddress, 0xDE90CC));
        Assert.NotEqual(a.Generation, b.Generation);
        NativeBattleEffect66VisualIdentity.Allocated(frame, fixedAddress);
        Assert.NotEqual(a.Generation, NativeBattleEffect66VisualIdentity.Point(frame, fixedAddress, 0xDE90CC).Generation);
        Assert.Equal(b, NativeBattleEffect66VisualIdentity.Point(frame, allocatedAddress, 0xDE477C));
    }
}
