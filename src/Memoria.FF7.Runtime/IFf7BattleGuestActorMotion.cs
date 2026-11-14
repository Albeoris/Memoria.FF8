namespace Memoria.FF7.Runtime;

/// <summary>A presentation owner leases the pose clock of a physical host actor without changing visibility.</summary>
public interface IFf7BattleGuestActorMotion
{
    bool IsActorPosePaused(int slot);
}

public sealed partial class Ff7BattleSession
{
    private bool IsGuestActorPosePaused(int slot)
    {
        return guestAttacks.OfType<IFf7BattleGuestActorMotion>().Any(owner => owner.IsActorPosePaused(slot));
    }
}
