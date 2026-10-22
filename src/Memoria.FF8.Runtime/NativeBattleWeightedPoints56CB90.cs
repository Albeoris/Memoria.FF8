namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWeightedPointsStep(NativeBattleEffectMemory Memory, NativeBattleVector Position);

public static class NativeBattleWeightedPoints56CB90
{
    public static NativeBattleWeightedPointsStep Blend(NativeBattleEffectMemory memory, NativeBattleVector first, NativeBattleVector second, short firstFactor, short secondFactor, NativeBattleSummonColors colors)
    {
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(int)firstFactor) } };
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)first.X), unchecked((ushort)first.Y), unchecked((ushort)first.Z));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, colors);
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(int)secondFactor) } };
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)second.X), unchecked((ushort)second.Y), unchecked((ushort)second.Z));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, colors);
        NativeBattleVector point = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
        return new(memory with { ScriptedColorRegisters = registers.Colors }, point);
    }
}
