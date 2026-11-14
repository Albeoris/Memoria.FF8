namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactAddressTests
{
    [Theory]
    [InlineData(0, 0x1D28344u)]
    [InlineData(48, 0x1D287C4u)]
    public void OriginalRowsHaveExactAddresses(int ordinal, uint address)
    {
        BattleContactOccurrence occurrence = new(ordinal);
        Assert.Equal(49, NativeBattleContactAddress.NativeRowCount);
        Assert.Equal(24, NativeBattleContactAddress.RowSize);
        Assert.Equal(address, NativeBattleContactAddress.EncodeOriginal(occurrence));
        Assert.Equal(occurrence, NativeBattleContactAddress.DecodeOriginal(address));
        Assert.True(NativeBattleContactAddress.TryDecodeOriginal(address, out BattleContactOccurrence decoded));
        Assert.Equal(occurrence, decoded);
        Assert.Equal(address, NativeBattleContactAddress.EncodeOriginalCursor(occurrence.AsCursor()));
        Assert.Equal(occurrence.AsCursor(), NativeBattleContactAddress.DecodeOriginalCursor(address));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(48)]
    public void FieldsResolveWithinAnOriginalRow(int ordinal)
    {
        BattleContactOccurrence occurrence = new(ordinal);
        uint address = NativeBattleContactAddress.EncodeOriginal(occurrence);
        Assert.Equal((occurrence, 0), NativeBattleContactAddress.ResolveOriginalField(address, 24));
        Assert.Equal((occurrence, 23), NativeBattleContactAddress.ResolveOriginalField(address + 23, 0));
        Assert.Equal(address + 23, NativeBattleContactAddress.EncodeOriginalField(occurrence, 23, 1));
        Assert.True(NativeBattleContactAddress.TryResolveOriginalField(address + 23, 1, out BattleContactOccurrence decoded, out int offset));
        Assert.Equal((occurrence, 23), (decoded, offset));
        Assert.False(NativeBattleContactAddress.TryDecodeOriginal(address + 1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.DecodeOriginal(address + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.EncodeOriginalField(occurrence, 24, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void InvalidFieldLengthsRejectWithoutAddressArithmetic(int byteLength)
    {
        const uint lastByte = 0x1D287DB;
        Assert.False(NativeBattleContactAddress.TryResolveOriginalField(lastByte, byteLength, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.ResolveOriginalField(lastByte, byteLength));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.EncodeOriginalField(new(48), 23, byteLength));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x1D28343u)]
    [InlineData(0x1D287DCu)]
    [InlineData(0x1D287F4u)]
    [InlineData(0xF2000000u)]
    [InlineData(0xF24AB068u)]
    [InlineData(uint.MaxValue)]
    public void UnownedAddressesRejectEvenZeroLengthFields(uint address)
    {
        Assert.False(NativeBattleContactAddress.TryDecodeOriginal(address, out _));
        Assert.False(NativeBattleContactAddress.TryResolveOriginalField(address, 0, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.ResolveOriginalField(address, 0));
    }

    [Fact]
    public void OnePastCursorDoesNotOwnARow()
    {
        const uint end = 0x1D287DC;
        BattleContactCursor cursor = new(49);
        Assert.Equal(end, NativeBattleContactAddress.EncodeOriginalCursor(cursor));
        Assert.Equal(cursor, NativeBattleContactAddress.DecodeOriginalCursor(end));
        Assert.False(NativeBattleContactAddress.TryDecodeOriginal(end, out _));
        Assert.False(NativeBattleContactAddress.TryResolveOriginalField(end, 0, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.EncodeOriginal(new(49)));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.EncodeOriginalCursor(cursor.Advance(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.DecodeOriginalCursor(0xF2000000));
    }

    [Fact]
    public void CursorOffsetRemainsWithinOriginalStorage()
    {
        Assert.Equal(0x1D287DCu, NativeBattleContactAddress.OffsetOriginalCursor(0x1D287C4, 1));
        Assert.Equal(0x1D287C4u, NativeBattleContactAddress.OffsetOriginalCursor(0x1D287DC, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.OffsetOriginalCursor(0x1D28344, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.OffsetOriginalCursor(0x1D287DC, 1));
        Assert.Throws<OverflowException>(() => NativeBattleContactAddress.OffsetOriginalCursor(0x1D287C4, int.MaxValue));
    }
}
