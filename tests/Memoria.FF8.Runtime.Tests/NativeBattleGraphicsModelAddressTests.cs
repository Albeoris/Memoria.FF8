namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsModelAddressTests
{
    private const uint NativeBase = 0x1D972C0;
    private const uint NativeEnd = 0x1D97704;
    private const uint FormerHostedBase = 0xF1000000;
    private const int RowSize = 156;

    [Fact]
    public void AllOriginalCellBasesRoundTripToTheExactNativeSlot()
    {
        Assert.Equal(7, NativeBattleGraphicsModelAddress.NativeRowCount);
        Assert.Equal(RowSize, NativeBattleGraphicsModelAddress.RowSize);
        for (byte index = 0; index < 7; index++)
        {
            NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(index);
            uint address = NativeBattleGraphicsModelAddress.EncodeOriginalCell(slot);
            Assert.Equal(NativeBase + (uint)(index * RowSize), address);
            Assert.Equal(slot, NativeBattleGraphicsModelAddress.DecodeOriginalCell(address));
            Assert.True(NativeBattleGraphicsModelAddress.TryDecodeOriginalCell(address, out NativeBattleModelSlot decoded));
            Assert.Equal(slot, decoded);
        }
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)6)]
    public void UnalignedFieldsResolveInsideOneCellAndRejectStraddles(byte index)
    {
        NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(index);
        uint address = NativeBattleGraphicsModelAddress.EncodeOriginalCell(slot);
        Assert.False(NativeBattleGraphicsModelAddress.TryDecodeOriginalCell(address + 37, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.DecodeOriginalCell(address + 37));
        Assert.Equal((slot, 37), NativeBattleGraphicsModelAddress.ResolveOriginalField(address + 37, RowSize - 37));
        Assert.Equal(address + 37, NativeBattleGraphicsModelAddress.EncodeOriginalField(slot, 37, RowSize - 37));
        Assert.Equal(address, NativeBattleGraphicsModelAddress.EncodeOriginalField(slot, 0, RowSize));
        Assert.Equal((slot, 155), NativeBattleGraphicsModelAddress.ResolveOriginalField(address + 155, 0));
        Assert.Equal(address + 155, NativeBattleGraphicsModelAddress.EncodeOriginalField(slot, 155, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeOriginalField(slot, RowSize, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeOriginalField(slot, 155, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveOriginalField(address + 155, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveOriginalField(address + 155, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeOriginalField(slot, 155, int.MaxValue));
    }

    [Fact]
    public void ZeroLengthAtTheNextCellBaseBelongsToThatCell()
    {
        NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(1);
        uint address = NativeBattleGraphicsModelAddress.EncodeOriginalCell(slot);
        Assert.Equal((slot, 0), NativeBattleGraphicsModelAddress.ResolveOriginalField(address, 0));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(NativeBase - 1)]
    [InlineData(NativeEnd)]
    [InlineData(NativeEnd + 1)]
    [InlineData(FormerHostedBase - 1)]
    [InlineData(FormerHostedBase)]
    [InlineData(FormerHostedBase + 18 * RowSize)]
    [InlineData(uint.MaxValue)]
    public void AModelAddressCannotLeaveTheOriginalWindow(uint address)
    {
        Assert.False(NativeBattleGraphicsModelAddress.TryDecodeOriginalCell(address, out _));
        Assert.False(NativeBattleGraphicsModelAddress.TryResolveOriginalField(address, out _, out int offset));
        Assert.Equal(-1, offset);
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.DecodeOriginalCell(address));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveOriginalField(address, 0));
    }

    [Fact]
    public void InvalidNativeSlotAndFieldInputsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelSlot.Decode(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelSlot.Decode(255));
        NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeOriginalField(slot, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.EncodeOriginalField(slot, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsModelAddress.ResolveOriginalField(NativeBase, -1));
    }
}
