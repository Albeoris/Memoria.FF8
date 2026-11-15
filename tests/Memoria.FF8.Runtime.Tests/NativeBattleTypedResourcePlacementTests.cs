namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTypedResourcePlacementTests
{
    [Fact]
    public void OriginalSlotWritersMapToTheirNativeBanksAndAddedOwnersUseIndependentResourceIds()
    {
        BattlePartyActorModel first = new(new("first"));
        BattlePartyActorModel second = new(new("second"));
        BattlePartyActorModel[] added = Enumerable.Range(0, 260).Select(index => new BattlePartyActorModel(new($"added-{index}"))).ToArray();
        BattleModelRoster roster = new([second, .. added, first]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(2), second), (NativeBattleModelSlot.Decode(0), first)]);
        NativeBattleModelCpuBanks banks = NativeBattleModelCpuBanks.Unknown().WithPresentationRoster(roster, aliases);

        Assert.Equal(0, banks.PartyBankIndex(first));
        Assert.Equal(2, banks.PartyBankIndex(second));
        Assert.Equal(5, banks.PartyBankIndex(added[0]));
        int last = banks.PartyBankIndex(added[^1]);
        Assert.True(last > byte.MaxValue);
        Assert.Null(new NativeBattleModelCpuReference(last, 0) { ResourceOnly = true }.TryPhysicalAddress());

        NativeBattleModelCpuBanks changed = banks.WithBank(last, banks.GetBank(last).Write(0, [17]));
        Assert.Equal(new byte[] { 17 }, changed.GetBank(last).Read(0, 1));
        Assert.Throws<NotSupportedException>(() => banks.GetBank(last).Read(0, 1));
        Assert.Throws<NotSupportedException>(() => changed.GetBank(5).Read(0, 1));
    }

    [Fact]
    public void NativeSlotRebindingRetiresItsOldLiveOwnerAndRetainsOriginalBytes()
    {
        BattlePartyActorModel original = new(new("old-generation"));
        BattlePartyActorModel replacement = new(new("new-generation"));
        NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(1);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown().BindPartyOwner(original, slot);
        cpu = cpu.BindAnimation(original, slot, new(0x3040, [45], 8));
        NativeBattleModelCpuResources rebound = cpu.BindPartyOwner(replacement, slot);

        Assert.Equal(1, rebound.Cpu.PartyBankIndex(replacement));
        Assert.Throws<InvalidDataException>(() => rebound.Cpu.PartyBankIndex(original));
        Assert.Empty(rebound.OwnedAnimationSources);
        Assert.Equal(new byte[] { 45 }, rebound.ReadAnimationHeader(40, 0, 1));
        Assert.Equal(new byte[] { 45 }, cpu.ReadAnimationSection(original, 8, 0, 1));
    }

    [Fact]
    public void ExtendedAnimationUsesItsTypedSourceWithoutAnOriginalDestination()
    {
        BattlePartyActorModel owner = new(new("added"));
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown().BindPartyOwner(owner, null);
        NativeBattleModelAnimationCopy copy = new(null, [8, 9, 10], 24);
        cpu = cpu.BindAnimation(owner, null, copy);

        Assert.Empty(cpu.AnimationSources);
        Assert.Null(copy.DestinationOffset);
        Assert.Equal(new byte[] { 9, 10 }, cpu.ReadAnimationSection(owner, 24, 1, 2));
        Assert.Throws<NotSupportedException>(() => cpu.ReadAnimationSection(owner, 8, 0, 1));
    }
}
