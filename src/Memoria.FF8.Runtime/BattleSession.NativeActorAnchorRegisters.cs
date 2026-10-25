namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleWaterActorAnchorServices CreateNativeActorAnchorServices()
    {
        return new(_ =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            return new(memory.GeometryRegisters ?? throw new NotSupportedException("Native actor anchor requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor anchor requires its current arithmetic registers."));
        }, (frame, registers) =>
        {
            NativeEffectMemory = RequireWaterMemory() with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
            return frame;
        });
    }
}
