namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect101TintStep(NativeBattleEffectMemory Memory, uint Rgb);

public static class NativeBattleEffect101Tint
{
    public static NativeBattleEffect101TintStep Execute(NativeBattleEffectMemory memory, uint savedColor, short factor, NativeBattleSummonColors constants)
    {
        NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown();
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), colors with { Ir0 = unchecked((uint)(int)factor), Ir = [(int)(savedColor & 255), (int)((savedColor >> 8) & 255), (int)((savedColor >> 16) & 255)] });
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, constants);
        uint rgb = 0;
        for (int lane = 0; lane < 3; lane++)
        {
            int value = registers.Colors.Ir[lane] ?? throw new InvalidDataException("Effect101 tint lost its reached GPF result.");
            rgb |= (uint)unchecked((byte)value) << (lane * 8);
        }
        return new(memory with { ScriptedColorRegisters = registers.Colors }, rgb);
    }
}
