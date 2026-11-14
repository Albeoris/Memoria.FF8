using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions : IFf7BattleGuestActorMotion
{
    public bool IsActorPosePaused(int slot)
    {
        if (fire is not { Presentation.Effect: { } effect } active)
        {
            return false;
        }
        CrossoverNativeMagicParticipant? participant = active.Presentation.BoundParticipants.SingleOrDefault(participant => participant.HostSlot == slot);
        return participant is not null && (effect.Graphics.Models[participant.ActorIndex].Flags0 & 8) != 0;
    }
}
