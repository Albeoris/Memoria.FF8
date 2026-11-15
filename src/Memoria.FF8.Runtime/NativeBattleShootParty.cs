namespace Memoria.FF8.Runtime;

public static class NativeBattleShootParty
{
    public static void RequireBallOwner(BattleActorModels models, NativeBattleModelBoundary bindings)
    {
        if (bindings.FindOriginalOwner(NativeBattleModelSlot.Decode(1), models) is not { } owner || (models.Get(owner).Flags0 & 2) == 0)
        {
            throw new InvalidDataException("SHOOT_PARTY_PRECONDITION: Shoot requires its original loaded physical party-slot1 ball owner; regenerate the diagnostic preparation with original party records before allocation.");
        }
    }
}
