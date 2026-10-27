using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicDisplay8ATests
{
    [Theory]
    [InlineData(0x008A, 6)]
    [InlineData(0xF08A, 6)]
    [InlineData(0x108A, 4)]
    [InlineData(0x208A, 4)]
    [InlineData(0x308A, 2)]
    public void Display_modes_publish_the_exact_parameter_bytes_or_selected_service_and_ignore_unreached_bytes(ushort instruction, uint advance)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0xFFFFFFFC);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797750, 0x1D99A78);
        frame = frame with { Arena = frame.Arena.Write(0x1000, [0x8A, 0, 0xFF, 0xFF, 3, 0]) };
        int mode = instruction >> 12;
        if (mode is 0 or 15)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1003, 1).MarkUnknown(0x1005, 1) };
        }
        else if (mode == 3)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 4) };
        }
        List<int> calls = [];
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicDisplay8A.Execute(current, new(0x1000, 6, 0xFFFFFFFC), new((address, byte83, byte81) =>
        {
            calls.Add(0);
            Assert.Equal(0x1D99A78u, address);
            Assert.Equal((byte)0, byte83);
            Assert.Equal((byte)3, byte81);
        }, duration =>
        {
            calls.Add(1);
            Assert.Equal((short)-1, duration);
        }, duration =>
        {
            calls.Add(2);
            Assert.Equal((short)-1, duration);
        }, () => calls.Add(3)));
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(new[] { mode is 0 or 15 ? 0 : mode }, calls);
        Assert.Equal(unchecked(0xFFFFFFFCu + advance), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(replayFrame)));
    }
}
