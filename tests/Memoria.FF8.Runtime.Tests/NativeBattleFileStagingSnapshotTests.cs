namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingSnapshotTests
{
    [Fact]
    public void SnapshotTransportsOnlyObservedBytesAcrossTheRetainedPhysicalOrigin()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x196A000, [1, 2]).Write(0x196A006, [7, 8]);
        (byte[] bytes, bool[] known) = staging.ReadSnapshot(0x1969FFE, 12);
        Assert.Equal(new byte[] { 0, 0, 1, 2, 0, 0, 0, 0, 7, 8, 0, 0 }, bytes);
        Assert.Equal(new bool[] { false, false, true, true, false, false, false, false, true, true, false, false }, known);
        Assert.Throws<NotSupportedException>(() => staging.Read(0x196A000, 8));
        Assert.Equal(0x196A000u, staging.BaseAddress);
        Assert.Throws<InvalidDataException>(() => staging.ReadSnapshot(uint.MaxValue, 2));
    }
}
