namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool CanSkipOrdinaryGuardianAnimation => NativeGraphics is { GuardianAnimationSkip: null, OriginalEffect: { ReturnedDescriptor: > 0 } effect, ActionGraphics: { } context }
        && ActiveAction is { NativeQueuedCommand: 254, GuardianFormInvocation: null }
        && nativeGuardianBoost is not null
        && NativeBattleGuardianAnimationFinale.IsSupported(effect.Binding.Effect)
        && nativeActionPackets.Headers[context.HeaderIndex].ReadByte(1) == 254;

    private bool SkippingOrdinaryGuardianAnimation
    {
        get
        {
            if (NativeGraphics is not { GuardianAnimationSkip: { FinaleReached: false } skip, OriginalEffect: { ReturnedDescriptor: > 0 } effect } || effect.Binding.Effect != skip.Effect)
            {
                return false;
            }
            return !NativeBattleGuardianAnimationFinale.HasReachedFinale(effect, NativeEffectMemory ?? throw new InvalidDataException("The active summon lost its effect memory."));
        }
    }

    private bool SkipOrdinaryGuardianAnimation()
    {
        if (!CanSkipOrdinaryGuardianAnimation)
        {
            return false;
        }
        NativeBattleGraphicsState graphics = NativeGraphics!;
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeGraphics = graphics with { GuardianAnimationSkip = new(effect.Binding.Effect, NativeBattleGuardianAnimationFinale.HasReachedFinale(effect, NativeEffectMemory!)) };
        SettleGuardianFormSkipBoost();
        return true;
    }

    private void RetainGuardianAnimationFinale()
    {
        if (NativeGraphics is { GuardianAnimationSkip: { FinaleReached: false } skip, OriginalEffect: { } effect } graphics
            && effect.Binding.Effect == skip.Effect
            && (effect.ReturnedDescriptor == 0 || NativeBattleGuardianAnimationFinale.HasReachedFinale(effect, NativeEffectMemory!)))
        {
            NativeGraphics = graphics with { GuardianAnimationSkip = skip with { FinaleReached = true } };
        }
    }
}
