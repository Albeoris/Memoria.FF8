namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicRibbonInputTests
{
    [Fact]
    public void ParentClearsOnlyZAndUsesRetainedXyRatherThanGlobalZero()
    {
        NativeBattleScriptedColorRegisters registers = NativeBattleScriptedColorRegisters.Unknown() with { NormalInput = [3, 0, 5, 0, 0xEE, 0xEE, 0xEE, 0xEE], NormalInputKnown = [true, true, true, true, false, false, false, false], Material = 0x3A000000 };
        NativeBattleActionMatrix basis = new([0, 4096, 0, 4096, 0, 0, 0, 0, -4096], 10, 20, 30, 4096);
        NativeBattleCinematicRibbonInputResult result = NativeBattleCinematicRibbonInput.PrepareParent(basis, registers);
        Assert.Equal((15, 23, 30), (result.Matrix.X, result.Matrix.Y, result.Matrix.Z));
        Assert.Equal(new byte[] { 3, 0, 5, 0, 0, 0, 0, 0 }, result.Registers.NormalInput!.Value);
        Assert.Null(result.Registers.NormalInputKnown);
        Assert.Equal(registers.Material, result.Registers.Material);
        Assert.Equal(registers.Flags, result.Registers.Flags);
    }

    [Fact]
    public void MissingRetainedXyFailsBeforeTranslation()
    {
        NativeBattleScriptedColorRegisters registers = NativeBattleScriptedColorRegisters.Unknown() with { NormalInput = [0, 0, 0, 0, 0, 0, 0, 0], NormalInputKnown = [true, true, false, true, true, true, true, true] };
        NativeBattleActionMatrix basis = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicRibbonInput.PrepareParent(basis, registers));
    }

    [Fact]
    public void ProjectionInputCapturePreservesUnknownPaddingMask()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = frame with { Arena = frame.Arena.Write(0x4000, new byte[] { 1, 2, 3, 4, 5, 6 }).MarkUnknown(0x4006, 2) };
        NativeBattleScriptedColorRegisters result = NativeBattleCinematicRibbonInput.Capture(frame, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000, NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, result.NormalInput!.Value.Take(6));
        Assert.Equal(new bool[] { true, true, true, true, true, true, false, false }, result.NormalInputKnown!.Value);
    }
}
