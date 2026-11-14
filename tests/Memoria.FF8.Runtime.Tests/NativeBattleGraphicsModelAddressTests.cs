namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsModelAddressTests
{
    private const uint NativeBase = 0x1D972C0;
    private const uint NativeEnd = 0x1D97704;
    private const uint HostedBase = 0xF1000000;
    private const int RowSize = 156;

    [Fact]
    public void OriginalAddressesStayExactAndHostedRowsRoundTripInTheirOwnRange()
    {
        Assert.Equal(7, NativeBattleGraphicsModelAddress.NativeRowCount);
        Assert.Equal(25, NativeBattleGraphicsModelAddress.MaximumRowCount);
        for (int row = 0; row < 25; row++)
        {
            uint expected = row < 7 ? NativeBase + (uint)(row * RowSize) : HostedBase + (uint)((row - 7) * RowSize);
            uint address = NativeBattleGraphicsModelAddress.Encode(row);
            Assert.Equal(expected, address);
            Assert.Equal(row, NativeBattleGraphicsModelAddress.Decode(address, 25));
            Assert.True(NativeBattleGraphicsModelAddress.TryDecode(address, out int decoded, row + 1));
            Assert.Equal(row, decoded);
            if (row < 7)
            {
                Assert.Equal(row, NativeBattleGraphicsModelAddress.Decode(address));
            }
            else
            {
                Assert.False(NativeBattleGraphicsModelAddress.TryDecode(address, out _));
                Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.Decode(address));
                Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(address, 1));
            }
            if (row > 0)
            {
                Assert.False(NativeBattleGraphicsModelAddress.TryDecode(address, out _, row));
                Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(address, 1, row));
            }
        }
    }

    [Fact]
    public void NativeEndAndAllFormerExtendedAliasesStayOutsideTheModelBank()
    {
        Assert.Equal(NativeEnd, NativeBase + 7 * RowSize);
        Assert.Equal(HostedBase, NativeBattleGraphicsModelAddress.Encode(7));
        Assert.NotEqual(NativeEnd, NativeBattleGraphicsModelAddress.Encode(7));
        // NativeEnd owns the original forced-camera bit. Wider banks never claim it.
        for (int row = 7; row < 25; row++)
        {
            uint address = NativeBase + (uint)(row * RowSize);
            Assert.False(NativeBattleGraphicsModelAddress.TryDecode(address, out _, 25));
            Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.Decode(address, 25));
            Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(address, 0, 25));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(24)]
    public void UnalignedFieldsResolveInsideOneRowAndRejectStraddles(int row)
    {
        uint address = NativeBattleGraphicsModelAddress.Encode(row);
        Assert.False(NativeBattleGraphicsModelAddress.TryDecode(address + 37, out _, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.Decode(address + 37, 25));
        Assert.Equal((row, 37), NativeBattleGraphicsModelAddress.ResolveField(address + 37, RowSize - 37, 25));
        Assert.Equal(address + 37, NativeBattleGraphicsModelAddress.EncodeField(row, 37, RowSize - 37));
        Assert.Equal(address, NativeBattleGraphicsModelAddress.EncodeField(row, 0, RowSize));
        Assert.Equal((row, 155), NativeBattleGraphicsModelAddress.ResolveField(address + 155, 0, 25));
        Assert.Equal(address + 155, NativeBattleGraphicsModelAddress.EncodeField(row, 155, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeField(row, RowSize, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeField(row, 155, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(address + 155, 2, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(address + 155, int.MaxValue, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeField(row, 155, int.MaxValue));
    }

    [Fact]
    public void ZeroLengthAtTheNextRowBaseBelongsOnlyToThatRow()
    {
        uint nextNative = NativeBattleGraphicsModelAddress.Encode(1);
        uint nextHosted = NativeBattleGraphicsModelAddress.Encode(8);
        Assert.Equal((1, 0), NativeBattleGraphicsModelAddress.ResolveField(nextNative, 0));
        Assert.Equal((8, 0), NativeBattleGraphicsModelAddress.ResolveField(nextHosted, 0, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(nextNative, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(nextHosted, 0, 8));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(NativeBase - 1)]
    [InlineData(NativeEnd + 1)]
    [InlineData(HostedBase - 1)]
    [InlineData(HostedBase + 18 * RowSize)]
    [InlineData(uint.MaxValue)]
    public void UnknownAddressesAndEndSentinelsAreRejected(uint address)
    {
        Assert.False(NativeBattleGraphicsModelAddress.TryDecode(address, out _, 25));
        Assert.False(NativeBattleGraphicsModelAddress.TryResolveField(address, out int actor, out int offset, 25));
        Assert.Equal(-1, actor);
        Assert.Equal(-1, offset);
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.Decode(address, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(address, 0, 25));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(26)]
    [InlineData(int.MaxValue)]
    public void AllDecodersRejectInvalidActorCounts(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.TryDecode(NativeBase, out _, count));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.TryResolveField(NativeBase, out _, out _, count));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.Decode(NativeBase, count));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(NativeBase, 1, count));
    }

    [Fact]
    public void TryResolveFieldIdentifiesOnlyTheStartByte()
    {
        Assert.True(NativeBattleGraphicsModelAddress.TryResolveField(NativeBase + 37, out int nativeActor, out int nativeOffset));
        Assert.Equal((0, 37), (nativeActor, nativeOffset));
        Assert.True(NativeBattleGraphicsModelAddress.TryResolveField(HostedBase + 155, out int hostedActor, out int hostedOffset, 8));
        Assert.Equal((7, 155), (hostedActor, hostedOffset));
        Assert.False(NativeBattleGraphicsModelAddress.TryResolveField(HostedBase, out hostedActor, out hostedOffset));
        Assert.Equal((-1, -1), (hostedActor, hostedOffset));
        Assert.False(NativeBattleGraphicsModelAddress.TryResolveField(NativeEnd, out _, out _, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(HostedBase + 155, 2, 8));
    }

    [Fact]
    public void EncodersRejectInvalidRowsOffsetsAndLengths()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.Encode(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.Encode(25));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeField(25, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeField(0, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeField(0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveField(NativeBase, -1));
    }
}
