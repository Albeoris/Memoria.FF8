namespace Memoria.FF8.Runtime;

public sealed record NativeBattleZantetsukenActorFadeStep(NativeBattleGraphicsModel Model, NativeBattleEffectMemory Memory);

public static class NativeBattleZantetsukenActorFade
{
    public static NativeBattleZantetsukenActorFadeStep Apply(NativeBattleGraphicsModel model, NativeBattleEffectMemory memory, int cue, NativeBattleSummonColors constants)
    {
        NativeBattlePrimitiveProcess process = (memory.PrimitiveProcess ?? throw new NotSupportedException("Zantetsuken fade requires its current renderer controls.")).PublishModelColor(constants.Transition);
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Zantetsuken fade did not publish its actual far-color controls.");
        NativeBattleScriptedDpcsResult faded = NativeBattleScriptedDpcs.Transform45F270(memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown(), 0, unchecked((uint)cue), far, constants);
        uint gray = unchecked((uint)(unchecked(cue << 7) >> 12));
        model = model with { Color44 = 0x32000000u | gray << 16 | gray << 8 | gray, Color40 = 0x02000000u | (faded.PackedRgb & 0xFFFFFF), Alpha7 = 0 };
        return new(model, memory with { PrimitiveProcess = process, ScriptedColorRegisters = faded.Registers });
    }
}
