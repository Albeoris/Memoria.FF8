using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorOrder9ATests
{
    [Fact]
    public void CopiesOnlyLowBytesAndKeepsThePhysicalWordStrideAcrossRestore()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x309A);
        frame = NativeBattleCinematicTypedTestFixture.WithContext(frame);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x20E0AB8);
        frame = frame with { Arena = frame.Arena.Write(0x1000, [0x9A, 0x30, 0x06, 0xBA, 0x04, 0xCD, 0x03, 0xEF]) };
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicActorOrder9A.Execute(current, new(0x1000, 8, 0x20E0AB8), NativeBattleCinematicTypedTestFixture.Services());
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal((byte)3, result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x41, 1)[0]);
        Assert.Equal(new byte[] { 6, 4, 3 }, result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x48, 3));
        Assert.Equal(0x20E0AC0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame)))));
    }

    [Fact]
    public void ZeroCountWritesTheCountAndAdvancesWithoutReadingAnOperand()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x9A);
        frame = NativeBattleCinematicTypedTestFixture.WithContext(frame);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0xFFFFFFFE);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorOrder9A.Execute(frame, new(-1, 0, 0), NativeBattleCinematicTypedTestFixture.Services());
        Assert.Equal((byte)0, result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x41, 1)[0]);
        Assert.Equal(0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }
}
