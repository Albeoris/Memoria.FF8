namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private short ReadNativeGraphicsSequenceVariable(NativeBattleGraphicsState graphics, byte variable)
    {
        return variable is 18 or 19 ? RequireNativeSequenceGlobals().ReadSequenceGlobal5044B0(variable) : ReadNativeGraphicsActionVariable(graphics, variable);
    }

    private void WriteNativeGraphicsSequenceVariable(byte variable, int value)
    {
        NativeEffectMemory = RequireNativeSequenceGlobals().WriteSequenceGlobal5048E0(variable, value);
    }

    private NativeBattleEffectMemory RequireNativeSequenceGlobals() => NativeEffectMemory ?? throw new NotSupportedException("Native sequence trigonometry requires its retained process memory owner.");
}
