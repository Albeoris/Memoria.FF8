using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterParticlesTests
{
    [Theory]
    [InlineData(0x8014)]
    [InlineData(0xC014)]
    [InlineData(0xA014)]
    [InlineData(0x9014)]
    [InlineData(0x8814)]
    [InlineData(0xF814)]
    [InlineData(0x4014)]
    [InlineData(0x2014)]
    [InlineData(0x1014)]
    [InlineData(0x0814)]
    [InlineData(0x7814)]
    [InlineData(0x0014)]
    public void Vector_copy14_uses_signed_cached_words_and_preserves_unselected_lanes_in_both_modes(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0);
        bool angles = (instruction & 0x8000) != 0;
        int source = angles ? 0x208C : 0x2094;
        int destination = angles ? 0x10 : 0x14;
        ushort[] values = [0x8000, 0xFFFF, 0x7FFF, 0xFFFE];
        ushort mask = angles ? instruction : unchecked((ushort)(instruction << 1));
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle + destination, 16) };
        for (int lane = 0; lane < 4; lane++)
        {
            frame = frame with { Arena = (mask & (0x8000 >> lane)) != 0 ? WriteWord(frame.Arena, source + lane * 2, values[lane]) : frame.Arena.MarkUnknown(source + lane * 2, 2) };
        }
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = One(frame);
        for (int lane = 0; lane < 4; lane++)
        {
            if ((mask & (0x8000 >> lane)) != 0)
            {
                Assert.Equal(unchecked((uint)((short)values[lane] << 8)), Dword(result, destination + lane * 4));
            }
            else
            {
                int offset = Particle + destination + lane * 4;
                Assert.Throws<NotSupportedException>(() => result.Arena.Read(offset, 4));
            }
        }
        Assert.Same(frame.Random, result.Random);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(One(replayFrame)));
        Assert.Throws<NotSupportedException>(() => One(frame with { Core = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000).Core, Arena = frame.Arena.MarkUnknown(0x1002, 2) }));
    }
}
