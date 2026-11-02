using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePartyCpuLayoutTests
{
    [Theory]
    [InlineData(NativeFieldRuntimeProfile.FF8EnExecutableHash, 0x196B018u, 0x10000)]
    [InlineData(NativeFieldRuntimeProfile.RemasteredEnImageHash, 0xA100000u, 0x20000)]
    public void WholeBankCopyRetainsAllocationAndUnknownBytes(string image, uint address, int length)
    {
        NativeBattlePartyCpuLayout layout = NativeBattlePartyCpuLayout.ForImage(image);
        NativeBattleModelCpuBanks cpu = NativeBattleModelCpuBanks.Unknown(layout);
        cpu = cpu with { Banks = cpu.Banks.SetItem(0, cpu.Banks[0].Write(length - 1, [17])).SetItem(2, cpu.Banks[2].Write(0, [23])) };
        NativeBattleModelCpuBanks copied = cpu.CopyBank50B2A0(0, 2);
        copied = JsonSerializer.Deserialize<NativeBattleModelCpuBanks>(JsonSerializer.Serialize(copied))!;
        copied.Validate();
        Assert.Equal(layout, copied.EffectiveLayout);
        Assert.Equal(length, copied.Banks[2].Capacity);
        Assert.Equal(new byte[] { 17 }, copied.Banks[2].Read(length - 1, 1));
        Assert.Throws<NotSupportedException>(() => copied.Banks[2].Read(0, 1));
        Assert.Throws<InvalidDataException>(() => copied.Banks[2].Write(length, [1]));
        Assert.Equal(address + (uint)(2 * length + length - 1), new NativeBattleModelCpuReference(2, length - 1) { Layout = layout }.PhysicalAddress);
    }

    [Fact]
    public void LegacySnapshotMeansClassicAndInvalidAllocationIsRejected()
    {
        NativeBattleModelCpuBanks legacy = JsonSerializer.Deserialize<NativeBattleModelCpuBanks>("{\"Banks\":[{\"KnownRanges\":[]},{\"KnownRanges\":[]},{\"KnownRanges\":[]}]}")!;
        legacy.Validate();
        Assert.Null(legacy.Layout);
        Assert.Equal(NativeBattlePartyCpuLayout.Classic, legacy.EffectiveLayout);
        Assert.Equal(0x198B018u, new NativeBattleModelCpuReference(2, 0).PhysicalAddress);
        Assert.Throws<InvalidDataException>(() => NativeBattlePartyCpuLayout.ForImage(new string('0', 64)));
        Assert.Throws<InvalidDataException>(() => (legacy with { Layout = NativeBattlePartyCpuLayout.Remastered }).Validate());
    }
}
