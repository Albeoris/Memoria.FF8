namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221Orbit11Tests
{
    [Theory]
    [InlineData(0, 100, 1100, 2000)]
    [InlineData(1024, 100, 1000, 2100)]
    [InlineData(-1024, 100, 1000, 1900)]
    [InlineData(1024, -100, 1000, 1900)]
    public void Orbit_uses_angleY_for_XY_and_linearZ_with_signed_reference_selector(int angle, int radius, ushort expectedX, ushort expectedY)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = frame with { Arena = frame.Arena.Write(0x205C, BitConverter.GetBytes(radius << 16)).Write(0x2060, BitConverter.GetBytes(angle << 16)).Write(0x2064, BitConverter.GetBytes(-20 << 16)) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xB0, unchecked((ushort)-7));
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 1000);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, 2000);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 10);
        NativeBattleWaterExecutionFrame result = NativeBattleApocalypse221Orbit11.Execute(frame, (owner, selector) =>
        {
            Assert.Same(frame, owner);
            Assert.Equal(-7, selector);
            return basis + 0x2100;
        });
        Assert.Equal(expectedX, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x94));
        Assert.Equal(expectedY, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x96));
        Assert.Equal(unchecked((ushort)-10), WaterSchedulerFixture.ReadSlotWord(result, 0, 0x98));
        Assert.Equal(frame.Core, result.Core);
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(frame.Random, result.Random);
    }
}
