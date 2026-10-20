namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221ActorTint69Tests
{
    [Theory]
    [InlineData(0, -1, 0x1234, 0x11223344u)]
    [InlineData(0, 0, 0x4D, 0x11223344u)]
    [InlineData(0, 128, 0x1A34, 0x02000080u)]
    [InlineData(3, -1, 0x1234, 0x11223344u)]
    [InlineData(3, 0, 0x4D, 0x11223344u)]
    [InlineData(-1, 128, 0x1A34, 0x02000080u)]
    public void Side_selection_null_headers_and_restore_hide_tint_modes_preserve_physical_owners(sbyte side, short red, ushort expectedFlags, uint expectedColor)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        const uint header = NativeBattleScriptedEffectMemory.Address;
        frame = frame with { Core = frame.Core.Write(header + 0x48, new[] { unchecked((byte)side) }) };
        for (int index = 0; index < 7; index++)
        {
            frame = WaterSchedulerFixture.CoreDword(frame, header + 0x60u + (uint)(index * 4), 0);
        }
        int actorIndex = side <= 2 ? 3 : 0;
        uint actorPointer = 0x1D972C0u + (uint)(actorIndex * 156);
        frame = WaterSchedulerFixture.CoreDword(frame, header + 0x60u + (uint)(actorIndex * 4), actorPointer);
        frame = WaterSchedulerFixture.CoreWord(frame, header + 0x4Eu + (uint)(actorIndex * 2), 0x1234);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)red));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 0);
        NativeBattleGraphicsModel actor = new(actorIndex, 0x45) { Color40 = 0x11223344, Color44 = 0x55667788 };
        int writes = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleApocalypse221ActorTint69.Execute(frame, new((_, address) =>
        {
            Assert.Equal(actorPointer, address);
            return actor;
        }, (owner, address, updated) =>
        {
            Assert.Equal(actorPointer, address);
            writes++;
            actor = updated;
            return owner;
        }));
        Assert.Equal(1, writes);
        Assert.Equal(expectedFlags, actor.Flags0);
        Assert.Equal(expectedColor, actor.Color40);
        Assert.Equal(0x55667788u, actor.Color44);
        Assert.Same(frame, result);
    }
}
