using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeRagnarokAuxiliaryRelocationTests
{
    [Fact]
    public void GenericRelocationUsesFirstAllocatedShapeAndRetainsFlagsPitchRollAndSignedYaw()
    {
        NativeWorldAuxiliaryRelocationReceipt receipt = new(1, NativeWorldAuxiliaryRelocationReceipt.RelocationRule, new string('A', 64), 4, 5, [new(0, 4, 48, new(20, -30, 40), -128), new(1, 5, 50, new(50, -60, 70), 127)], new(32, 50, -1), new(1, 2, -3, 4));
        ImmutableArray<NativeRagnarokWorldObject> before = [new(64, 7, default, 8, 9, 10), new(65, 2, default, 11, 12, 13), new(1, 3, default, 14, 15, 16), new(1, 4, default, 17, 18, 19)];
        receipt.RequireRagnarokPublication(receipt.Packet, receipt.FinalLoadPose);
        ImmutableArray<NativeRagnarokWorldObject> after = receipt.RelocateRagnarokObjects(before);
        Assert.Equal(before[0] with { Position = new(20, -30, 40), Yaw = -128 }, after[0]);
        Assert.Equal(before[1] with { Position = new(20, -30, 40), Yaw = -128 }, after[1]);
        Assert.Equal(before[2] with { Position = new(50, -60, 70), Yaw = 127 }, after[2]);
        Assert.Equal(before[3], after[3]);
        Assert.Equal(default, before[0].Position);
    }

    [Fact]
    public void MissingActualTargetAndWrongPublicationAreRefusedWithoutChangingSourceObjects()
    {
        NativeWorldAuxiliaryRelocationReceipt receipt = new(1, NativeWorldAuxiliaryRelocationReceipt.RelocationRule, new string('A', 64), 1, 0, [new(0, 1, 2, new(1, 2, 3), 1)], new(32, 50, -1), new(1, 2, -3, 4));
        ImmutableArray<NativeRagnarokWorldObject> before = [new(1, 0, default, 0, 0, 0)];
        Assert.Throws<NotSupportedException>(() => receipt.RelocateRagnarokObjects(before));
        Assert.Equal(default, before[0].Position);
        Assert.Throws<NotSupportedException>(() => receipt.RequireRagnarokPublication(new(32, 48, -1), receipt.FinalLoadPose));
    }
}
