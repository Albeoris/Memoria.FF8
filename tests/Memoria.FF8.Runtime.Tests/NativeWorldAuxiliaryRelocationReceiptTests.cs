using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldAuxiliaryRelocationReceiptTests
{
    [Fact]
    public void ConsumptionClearsOrderedPhysicalChannelsWithoutChangingOtherEventBytes()
    {
        NativeWorldAuxiliaryRelocationReceipt receipt = Fixture();
        NativeEventMemory before = NativeEventMemory.Empty.Write(269, 2, NativeScalarValue.Exact(0x0101)).Write(271, 1, NativeScalarValue.Exact(7));
        NativeEventMemory after = receipt.ApplyTo(before);
        Assert.Equal(0, after.Read(269, 2).RequireExact());
        Assert.Equal(7, after.Read(271, 1).RequireExact());
        Assert.Equal(0x0101, before.Read(269, 2).RequireExact());
        Assert.Throws<InvalidDataException>(() => receipt.ApplyTo(after));
        Assert.Throws<InvalidDataException>(() => (receipt with { Relocations = receipt.Relocations.Reverse().ToImmutableArray() }).Validate());
    }

    [Fact]
    public void ExactRestorePreservesTheFinalGardenPublicationAndSignedByteYaw()
    {
        NativeWorldAuxiliaryRelocationReceipt receipt = Fixture();
        NativeWorldAuxiliaryRelocationReceipt restored = JsonSerializer.Deserialize<NativeWorldAuxiliaryRelocationReceipt>(JsonSerializer.Serialize(receipt))!;
        restored.RequireGardenPublication(receipt.Packet, receipt.FinalLoadPose);
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(restored));
        Assert.Equal((short)-128, restored.Relocations[1].Yaw);
        Assert.Throws<NotSupportedException>(() => restored.RequireGardenPublication(new(62, 0, -1), receipt.FinalLoadPose));
        Assert.Throws<NotSupportedException>(() => (restored with { Relocations = restored.Relocations.SetItem(1, restored.Relocations[1] with { EntityType = 0 }) }).RequireGardenPublication(receipt.Packet, receipt.FinalLoadPose));
    }

    private static NativeWorldAuxiliaryRelocationReceipt Fixture()
    {
        ImmutableArray<NativeWorldAuxiliaryRelocation> rows = [new(0, 1, 48, new(15769, -659, 29052), 9), new(1, 1, 48, new(15769, -659, 29052), -128)];
        return new(1, NativeWorldAuxiliaryRelocationReceipt.RelocationRule, new string('A', 64), 1, 1, rows, new(62, 48, -1), new(16533, -24027, -163, 712));
    }
}
