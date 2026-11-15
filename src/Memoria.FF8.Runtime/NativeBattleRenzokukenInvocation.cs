namespace Memoria.FF8.Runtime;

internal static class NativeBattleRenzokukenInvocation
{
    internal static BattleModelOwner Source(NativeBattleOriginalEffectState effect) => (effect.Parameters ?? throw new NotSupportedException("Native Renzokuken requires its current action parameters.")).Source;

    internal static BattleModelOwner FirstTarget(NativeBattleOriginalEffectState effect) => effect.EntryFirstRecipient ?? throw new NotSupportedException("Native Renzokuken requires its captured first recipient.");
}
