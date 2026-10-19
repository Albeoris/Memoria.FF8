using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterParticlesTests
{
    [Theory]
    [InlineData(0, 0, 1, 0)]
    [InlineData(256, -512, -4, 6)]
    [InlineData(-1000, 250, -2, -3)]
    public void AimAtBoneUsesActualResolvedXZAndPreservesNativeAngleScratch(int x, int z, short targetX, short targetZ)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x000E, 0xFFFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x14, 99);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 99);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, unchecked((ushort)targetX));
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, unchecked((ushort)targetZ));
        frame = frame with { Core = frame.Core.Write(0x2797454, [1, 0xFF]), Arena = frame.Arena.MarkUnknown(0x2196, 2) };
        frame = PutDword(PutDword(frame, 0x14, unchecked((uint)x)), 0x1C, unchecked((uint)z));
        (NativeBattleWaterExecutionFrame expected, int angle) = NativeBattleCinematicRibbonBasis.Angle(frame, x, z, targetX << 8, targetZ << 8);
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Equal(unchecked((uint)(angle << 4)), Dword(result, 0x1C));
        Assert.Equal(unchecked((uint)x), Dword(result, 0x14));
        Assert.Equal(expected.ReadTemporaryDword(0xFC), result.ReadTemporaryDword(0xFC));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }
}
