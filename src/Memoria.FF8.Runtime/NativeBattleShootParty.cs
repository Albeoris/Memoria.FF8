namespace Memoria.FF8.Runtime;

public static class NativeBattleShootParty
{
    public static void RequireBallOwner(IReadOnlyList<NativeBattleGraphicsModel> models)
    {
        if (models.Count != 7 || (models[1].Flags0 & 2) == 0)
        {
            throw new InvalidDataException("SHOOT_PARTY_PRECONDITION: Shoot requires its original loaded physical party-slot1 ball owner; regenerate the diagnostic preparation with original party records before allocation.");
        }
    }
}
