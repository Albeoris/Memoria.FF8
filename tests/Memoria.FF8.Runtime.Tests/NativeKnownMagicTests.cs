using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeKnownMagicTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    public void SavedBitsUseOriginalSpellMinusOneIndexAndMonotonicReceipt(int spell)
    {
        NativeKnownMagic before = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0));
        NativeKnownMagic after = before.Add(spell);
        Assert.True(after.Contains(spell));
        Assert.False(before.Contains(spell));
        NativeKnownMagic.ValidateReceipt(before, after);
        Assert.Throws<InvalidDataException>(() => NativeKnownMagic.ValidateReceipt(after, before));
    }

    [Fact]
    public void PartyStockIsDiscoveredAtEntryAndMissingSnapshotBankIsRejected()
    {
        NativeKnownMagic before = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0));
        BattleSession battle = new(new BattleDefinition
        {
            NativeKnownMagic = before,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MagicStocks = [new(32, 1)] }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        Assert.True(battle.KnownMagic!.Contains(32));
        Assert.False(before.Contains(32));
        Assert.True(BattleSession.Restore(battle.Capture()).KnownMagic!.Contains(32));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(battle.Capture() with { NativeKnownMagic = null }));
    }
}
