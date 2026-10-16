namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeModuleEffects(NativeBattleGraphicsState graphics)
    {
        NativeBattleModuleStep step = graphics.ModuleEffects.Advance(new(graphics.Flags) { Resources = definition.NativeModuleResources, CrtRandom = nativeCrtRandom, ReadModelAnchor = index => ReadNativeModelAnchor(graphics, index), ReadModelRotation14 = index => graphics.Models[index].Rotation14 ?? throw new NotSupportedException("Native module effect requires the loaded model rotationWORD14.") });
        nativeCrtRandom = step.CrtRandomAfter;
        return graphics with { ModuleEffects = step.State, ModuleDraws = step.Draws };
    }
}
