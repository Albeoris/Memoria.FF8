namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{

    private NativeBattleGraphicsState CompleteNativeActivation(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleModelSlot? originalSlot = null)
    {
        if (hostedFrameServices is { } services)
        {
            if (!graphics.ActorModels.Contains(owner) || !services.Facts.TryGetValue(owner, out NativeBattleModelActorFacts? actorFacts))
            {
                throw new InvalidDataException("Hosted activation lost its allocated model owner.");
            }
            services.Emit(new NativeBattleModelParticipantActivated(owner, actorFacts.ActorId) { OriginalSlot = originalSlot });
            return graphics;
        }
        return CompleteEffectPresentationActivation(graphics, owner);
    }

    private NativeBattleGraphicsState CompleteNativeAllocationDescriptor(NativeBattleGraphicsState graphics)
    {
        (hostedFrameServices ?? throw new InvalidOperationException("Native allocation completion requires its frame receipt owner.")).EmitAllocationGate();
        return graphics;
    }
}
