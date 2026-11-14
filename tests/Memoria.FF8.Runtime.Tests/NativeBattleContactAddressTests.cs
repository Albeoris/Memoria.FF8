namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactAddressTests
{
    [Theory]
    [InlineData(0, 0x1D28344u)]
    [InlineData(48, 0x1D287C4u)]
    [InlineData(49, 0xF2000000u)]
    [InlineData(50, 0xF2000018u)]
    [InlineData(203999, 0xF24AB050u)]
    public void PhysicalAndHostedRowsHaveExactAddresses(int row, uint address)
    {
        Assert.Equal(49, NativeBattleContactAddress.NativeRowCount);
        Assert.Equal(204000, NativeBattleContactAddress.MaximumRowCount);
        Assert.Equal(24, NativeBattleContactAddress.RowSize);
        Assert.Equal(address, NativeBattleContactAddress.Encode(row));
        Assert.Equal(row, NativeBattleContactAddress.Decode(address, row + 1));
        Assert.True(NativeBattleContactAddress.TryDecode(address, out int decoded, row + 1));
        Assert.Equal(row, decoded);
        Assert.Equal(row < 49, NativeBattleContactAddress.TryDecode(address, out _));
    }

    [Theory]
    [InlineData(48)]
    [InlineData(49)]
    [InlineData(203999)]
    public void FieldsResolveWithinAnOwnedRow(int row)
    {
        uint address = NativeBattleContactAddress.Encode(row);
        Assert.Equal((row, 0), NativeBattleContactAddress.ResolveField(address, 24, row + 1));
        Assert.Equal((row, 23), NativeBattleContactAddress.ResolveField(address + 23, 0, row + 1));
        Assert.Equal(address + 23, NativeBattleContactAddress.EncodeField(row, 23, 1));
        Assert.True(NativeBattleContactAddress.TryResolveField(address + 23, 1, out int contact, out int offset, row + 1));
        Assert.Equal((row, 23), (contact, offset));
        Assert.False(NativeBattleContactAddress.TryDecode(address + 1, out _, row + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.Decode(address + 1, row + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.EncodeField(row, 24, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void InvalidFieldLengthsRejectWithoutAddressArithmetic(int byteLength)
    {
        const uint lastByte = 0x1D287DB;
        Assert.False(NativeBattleContactAddress.TryResolveField(lastByte, byteLength, out int contact, out int offset));
        Assert.Equal((-1, -1), (contact, offset));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.ResolveField(lastByte, byteLength));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.EncodeField(48, 23, byteLength));
    }

    [Theory]
    [InlineData(0u, 204000)]
    [InlineData(0x1D28343u, 204000)]
    [InlineData(0x1D287DCu, 204000)]
    [InlineData(0x1D287F4u, 204000)]
    [InlineData(0xF1FFFFFFu, 204000)]
    [InlineData(0xF2000000u, 49)]
    [InlineData(0xF2000018u, 50)]
    [InlineData(0xF24AB068u, 204000)]
    [InlineData(uint.MaxValue, 204000)]
    public void UnownedAddressesRejectEvenZeroLengthFields(uint address, int capacity)
    {
        Assert.False(NativeBattleContactAddress.TryDecode(address, out _, capacity));
        Assert.False(NativeBattleContactAddress.TryResolveField(address, 0, out int contact, out int offset, capacity));
        Assert.Equal((-1, -1), (contact, offset));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.ResolveField(address, 0, capacity));
    }

    [Theory]
    [InlineData(48, 0x1D287C4u)]
    [InlineData(49, 0x1D287DCu)]
    [InlineData(50, 0xF2000018u)]
    [InlineData(204000, 0xF24AB068u)]
    public void OnePastCursorDoesNotOwnARow(int capacity, uint end)
    {
        Assert.Equal(end, NativeBattleContactAddress.EncodeCursor(capacity, capacity));
        Assert.Equal(capacity, NativeBattleContactAddress.DecodeCursor(end, capacity));
        Assert.False(NativeBattleContactAddress.TryDecode(end, out _, capacity));
        Assert.False(NativeBattleContactAddress.TryResolveField(end, 0, out _, out _, capacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.EncodeCursor(capacity + 1, capacity));
    }

    [Fact]
    public void NativeEndCursorDoesNotAliasTheFirstHostedRow()
    {
        Assert.Equal(49, NativeBattleContactAddress.DecodeCursor(0x1D287DC));
        Assert.Equal(0xF2000000u, NativeBattleContactAddress.EncodeCursor(49, 50));
        Assert.Equal(49, NativeBattleContactAddress.DecodeCursor(0xF2000000, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.DecodeCursor(0x1D287DC, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.DecodeCursor(0xF2000000));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.DecodeCursor(0xF2000001, 50));
    }

    [Fact]
    public void CursorOffsetCrossesBetweenAddressSegmentsInBothDirections()
    {
        Assert.Equal(0xF2000000u, NativeBattleContactAddress.OffsetCursor(0x1D287C4, 1, 50));
        Assert.Equal(0x1D287C4u, NativeBattleContactAddress.OffsetCursor(0xF2000000, -1, 50));
        Assert.Equal(0xF2000018u, NativeBattleContactAddress.OffsetCursor(0x1D287C4, 2, 50));
        Assert.Equal(0x1D287C4u, NativeBattleContactAddress.OffsetCursor(0xF2000018, -2, 50));
        Assert.Equal(0x1D287DCu, NativeBattleContactAddress.OffsetCursor(0x1D287C4, 1));
        Assert.Equal(0x1D287C4u, NativeBattleContactAddress.OffsetCursor(0x1D287DC, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.OffsetCursor(0x1D287DC, 0, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.OffsetCursor(0xF2000018, 1, 50));
        Assert.Throws<OverflowException>(() => NativeBattleContactAddress.OffsetCursor(0x1D287C4, int.MaxValue, 50));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(204001)]
    public void InvalidCapacitiesReject(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.TryDecode(0x1D28344, out _, capacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.TryResolveField(0x1D28344, 1, out _, out _, capacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.EncodeCursor(0, capacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.DecodeCursor(0x1D28344, capacity));
    }

    [Fact]
    public void OnlyCursorEncodingCanReachTheCapacityBoundary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.Encode(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.Encode(204000));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleContactAddress.EncodeCursor(-1));
    }
}
