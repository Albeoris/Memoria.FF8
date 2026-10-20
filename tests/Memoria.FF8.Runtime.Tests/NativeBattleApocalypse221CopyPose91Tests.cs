namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221CopyPose91Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(0x15)]
    [InlineData(0x3F)]
    [InlineData(-1)]
    public void Six_mask_bits_copy_signed_WORDs_skip_padding_then_cache_before_actual_bone_update(short mask)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x91);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, basis + 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(mask)).Write(0x1004, BitConverter.GetBytes((short)-7)).Write(0x2018, new byte[] { 11 }).MarkUnknown(0x2192, 2) };
        short[] selected = [-32768, -1, 32767, -300, 42, 1234];
        for (int lane = 0; lane < 6; lane++)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50 + lane * 4, (uint)((lane + 1) << 16));
            int offset = 0x8C + lane * 2 + (lane >= 3 ? 2 : 0);
            frame = WaterSchedulerFixture.SlotWord(frame, 1, offset, unchecked((ushort)selected[lane]));
            if ((mask & (1 << lane)) == 0)
            {
                frame = frame with { Arena = frame.Arena.MarkUnknown(0x2100 + offset, 2) };
            }
        }
        int callbacks = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCopyPose91.Execute(frame, new(0x1000, 6, basis + 0x1000), (owner, selector) =>
        {
            Assert.Same(frame, owner);
            Assert.Equal(-7, selector);
            return basis + 0x2100;
        }, (owner, kind) =>
        {
            callbacks++;
            Assert.Equal((byte)11, kind);
            for (int lane = 0; lane < 6; lane++)
            {
                short expected = (mask & (1 << lane)) != 0 ? selected[lane] : (short)(lane + 1);
                Assert.Equal(unchecked((uint)(expected << 16)), WaterSchedulerFixture.ReadSlotDword(owner, 0, 0x50 + lane * 4));
                if (lane < 3)
                {
                    Assert.Equal(unchecked((ushort)expected), WaterSchedulerFixture.ReadSlotWord(owner, 0, 0x8C + lane * 2));
                }
            }
            return WaterSchedulerFixture.CoreDword(owner, 0x2797450, basis + 0x1008);
        });
        Assert.Equal(1, callbacks);
        Assert.Equal(basis + 0x100E, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Random, result.Random);
    }
}
