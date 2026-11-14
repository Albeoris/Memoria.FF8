using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelAliasEncodingTests
{
    private static readonly BattlePartyActorModel Party = new(new("party"));
    private static readonly BattleEnemyActorModel Enemy = new(new("enemy"));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void OriginalOperandsZeroExtendTheExactAlias(int width)
    {
        NativeBattleModelAliases aliases = OriginalAliases();
        byte[] expectedParty = new byte[width];
        expectedParty[0] = 2;
        byte[] expectedEnemy = new byte[width];
        expectedEnemy[0] = 6;

        Assert.Equal(expectedParty, aliases.EncodeOperand(Party, width)!.Value.ToArray());
        Assert.Equal(expectedEnemy, aliases.EncodeOperand(Enemy, width)!.Value.ToArray());
    }

    [Fact]
    public void OriginalCellAddressesUseTheExactLittleEndianAddress()
    {
        NativeBattleModelAliases aliases = OriginalAliases();

        Assert.Equal(new byte[] { 0xF8, 0x73, 0xD9, 0x01 }, aliases.EncodeAddress(Party)!.Value.ToArray());
        Assert.Equal(new byte[] { 0x68, 0x76, 0xD9, 0x01 }, aliases.EncodeAddress(Enemy)!.Value.ToArray());
    }

    [Fact]
    public void PresentExtraAndForeignOwnersHaveNoNativeBytesWithoutAnAlias()
    {
        BattlePartyActorModel extraParty = new(new("extra-party"));
        BattleEnemyActorModel extraEnemy = new(new("extra-enemy"));
        BattleForeignPartyModel foreignParty = new(new("foreign-party"));
        BattleForeignEnemyModel foreignEnemy = new(new("foreign-enemy"));
        BattleModelOwner[] unaliased = [extraParty, extraEnemy, foreignParty, foreignEnemy];
        BattleModelRoster roster = new(new BattleModelOwner[] { Party, Enemy }.Concat(unaliased));
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(2), Party), (NativeBattleModelSlot.Decode(6), Enemy)]);

        foreach (BattleModelOwner owner in unaliased)
        {
            Assert.Null(aliases.EncodeOperand(owner, 1));
            Assert.Null(aliases.EncodeOperand(owner, 2));
            Assert.Null(aliases.EncodeOperand(owner, 4));
            Assert.Null(aliases.EncodeAddress(owner));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(8)]
    public void UnsupportedWidthRejectsEvenWhenABoundOwnerHasNoAlias(int width)
    {
        NativeBattleModelAliases aliases = new(new([Party]), []);

        Assert.Throws<ArgumentOutOfRangeException>(() => aliases.EncodeOperand(Party, width));
    }

    [Fact]
    public void WriterSnapshotRetainsItsOldBytesAfterAnOriginalCellIsReused()
    {
        NativeBattleModelAliases prior = OriginalAliases();
        ImmutableArray<byte> priorPartyOperand = prior.EncodeOperand(Party, 4)!.Value;
        ImmutableArray<byte> priorPartyAddress = prior.EncodeAddress(Party)!.Value;
        ImmutableArray<byte> priorEnemyOperand = prior.EncodeOperand(Enemy, 4)!.Value;
        ImmutableArray<byte> priorEnemyAddress = prior.EncodeAddress(Enemy)!.Value;

        BattleEnemyActorModel replacement = new(new("replacement"));
        BattleForeignEnemyModel unboundForeign = new(new("outside-this-writer"));
        NativeBattleModelAliases current = new(new([Party, replacement]), [(NativeBattleModelSlot.Decode(0), Party), (NativeBattleModelSlot.Decode(6), replacement)]);
        Assert.Throws<InvalidDataException>(() => current.EncodeOperand(Enemy, 4));
        Assert.Throws<InvalidDataException>(() => current.EncodeAddress(Enemy));
        Assert.Throws<InvalidDataException>(() => current.EncodeOperand(unboundForeign, 4));
        Assert.Throws<InvalidDataException>(() => current.EncodeAddress(unboundForeign));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, current.EncodeOperand(Party, 4)!.Value.ToArray());
        Assert.Equal(new byte[] { 0xC0, 0x72, 0xD9, 0x01 }, current.EncodeAddress(Party)!.Value.ToArray());
        Assert.Equal(new byte[] { 2, 0, 0, 0 }, priorPartyOperand.ToArray());
        Assert.Equal(new byte[] { 0xF8, 0x73, 0xD9, 0x01 }, priorPartyAddress.ToArray());
        Assert.Equal(priorEnemyOperand.ToArray(), current.EncodeOperand(replacement, 4)!.Value.ToArray());
        Assert.Equal(priorEnemyAddress.ToArray(), current.EncodeAddress(replacement)!.Value.ToArray());
        Assert.Equal(Enemy, prior.DecodeLiteral(6));
        Assert.Equal(replacement, current.DecodeLiteral(6));
    }

    [Fact]
    public void BoundaryCapturesActualBindingsAndRejectsAModelBankMissingABoundOwner()
    {
        BattleEnemyActorModel extra = new(new("extra"));
        BattleEnemyActorModel replacement = new(new("replacement"));
        NativeBattleGraphicsModel cell = new(0);
        NativeBattleModelBoundary boundary = new(Enumerable.Repeat(cell, 7));
        BattleActorModels models = new([new(extra, cell)]);
        NativeBattleModelSlot originalSlot = NativeBattleModelSlot.Decode(6);
        (boundary, models) = boundary.BindOriginalSlot(originalSlot, Enemy, models);
        NativeBattleModelAliases before = boundary.CaptureAliases(models);
        Assert.Same(models.Roster, before.Roster);
        Assert.Equal(Enemy, before.DecodeLiteral(6));
        Assert.Null(before.EncodeAddress(extra));
        Assert.Throws<InvalidDataException>(() => before.DecodeLiteral(3));
        Assert.Throws<InvalidDataException>(() => boundary.CaptureAliases(models.Remove(Enemy)));

        (boundary, models) = boundary.BindOriginalSlot(originalSlot, replacement, models);
        NativeBattleModelAliases after = boundary.CaptureAliases(models);
        Assert.Equal(replacement, after.DecodeLiteral(6));
        Assert.Throws<InvalidDataException>(() => after.EncodeAddress(Enemy));
        Assert.Null(after.EncodeAddress(extra));
        Assert.Equal(Enemy, before.DecodeLiteral(6));
    }

    private static NativeBattleModelAliases OriginalAliases() =>
        new(new([Party, Enemy]), [(NativeBattleModelSlot.Decode(2), Party), (NativeBattleModelSlot.Decode(6), Enemy)]);
}
