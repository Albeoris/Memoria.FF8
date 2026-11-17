namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal bool CanSkipOrdinaryGuardianAnimation => NativeGraphics is { GuardianAnimationSkip: null, OriginalEffect: { ReturnedDescriptor: > 0 } effect, ActionGraphics: { } context }
        && hostedPresentationInvocation is { } active && active.Header == context.Header
        && NativeGraphics.GuardianFormPresentation?.Mode != GuardianFormInvocationMode.FinisherWithoutEntry
        && NativeBattleGuardianAnimationFinale.IsSupported(effect.Binding.Effect)
        && nativeActionPackets.ReadHeader(context.Header).Command == 254;

    internal bool SkippingOrdinaryGuardianAnimation
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

    internal bool SkipOrdinaryGuardianAnimation()
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

    internal void RetainGuardianAnimationFinale()
    {
        if (NativeGraphics is { GuardianAnimationSkip: { FinaleReached: false } skip, OriginalEffect: { } effect } graphics
            && effect.Binding.Effect == skip.Effect
            && (effect.ReturnedDescriptor == 0 || NativeBattleGuardianAnimationFinale.HasReachedFinale(effect, NativeEffectMemory!)))
        {
            NativeGraphics = graphics with { GuardianAnimationSkip = skip with { FinaleReached = true } };
        }
    }
}
