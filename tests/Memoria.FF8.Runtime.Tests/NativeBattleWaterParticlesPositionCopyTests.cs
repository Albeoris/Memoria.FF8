using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterParticlesTests
{
    [Theory]
    [InlineData(0x4015)]
    [InlineData(0x2015)]
    [InlineData(0x1015)]
    [InlineData(0x7015)]
    public void Position_copy15_reads_only_selected_cached_WORD_lanes_and_keeps_the_other_particle_words(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle + 0x22, 6) };
        ushort[] expected = [0x8000, 0xFFFF, 0x7FFF];
        for (int lane = 0; lane < 3; lane++)
        {
            int offset = 0x2094 + lane * 2;
            frame = frame with { Arena = (instruction & (0x4000 >> lane)) != 0 ? frame.Arena.Write(offset, BitConverter.GetBytes(expected[lane])) : frame.Arena.MarkUnknown(offset, 2) };
        }
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = One(frame);
        for (int lane = 0; lane < 3; lane++)
        {
            if ((instruction & (0x4000 >> lane)) != 0)
            {
                Assert.Equal(expected[lane], Word(result, 0x22 + lane * 2));
            }
            else
            {
                int offset = Particle + 0x22 + lane * 2;
                Assert.Throws<NotSupportedException>(() => result.Arena.Read(offset, 2));
            }
        }
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Random, result.Random);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(One(replayFrame)));
    }

    [Fact]
    public void Empty_position_copy15_still_reads_its_selector_but_does_not_touch_destination_or_consume_random()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x15, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle, 0x50).MarkUnknown(0x2094, 6) };
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle + 0x22, 2));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        Assert.Throws<NotSupportedException>(() => One(frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) }));
    }
}
