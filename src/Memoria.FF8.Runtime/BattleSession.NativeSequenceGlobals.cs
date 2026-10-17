namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private int ReadNativeGraphicsSequenceVariable(NativeBattleGraphicsState graphics, int modelIndex, byte variable)
    {
        if (variable == 40)
        {
            return (NativeEffectMemory ?? throw new NotSupportedException("Native read40 requires its process task pointer owner.")).ReadReturnedTask5044B0();
        }
        if (variable == 41)
        {
            return NativeBattleActionGraphics.ReadFirstHeaderTargetCount5044B0(graphics.ActionGraphics);
        }
        if (variable == 46)
        {
            return NativeBattleActionGraphics.AnyOtherModelBusy5044B0(graphics.Models, modelIndex);
        }
        if (variable == 51)
        {
            return unchecked((ushort)(EncounterId ?? throw new NotSupportedException("Native sequence encounter query requires its original encounter identity.")));
        }
        if (variable is 53 or 54)
        {
            int target = (graphics.ActionContext ?? throw new NotSupportedException("Native target bone query requires its50D300 target identity.")).TargetIndex ?? throw new NotSupportedException("Native target bone query requires its physical target identity.");
            NativeBattleVector bone = ReadNativeGraphicsBone(graphics, target, variable == 53 ? (byte)241 : (byte)240, 2048);
            NativeBattleModelTransform transform = graphics.Models[target].Transform ?? throw new NotSupportedException("Native target bone query requires its target position WORD30.");
            return bone.Y - transform.Position28.Y;
        }
        return variable is 18 or 19 ? RequireNativeSequenceGlobals().ReadSequenceGlobal5044B0(variable) : ReadNativeGraphicsActionVariable(graphics, variable);
    }

    private void WriteNativeGraphicsSequenceVariable(byte variable, int value)
    {
        NativeEffectMemory = RequireNativeSequenceGlobals().WriteSequenceGlobal5048E0(variable, value);
    }

    private NativeBattleEffectMemory RequireNativeSequenceGlobals() => NativeEffectMemory ?? throw new NotSupportedException("Native sequence trigonometry requires its retained process memory owner.");
}
