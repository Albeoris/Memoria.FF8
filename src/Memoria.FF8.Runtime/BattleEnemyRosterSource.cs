namespace Memoria.FF8.Runtime;

/// <summary>Immutable source placement for an enemy added from an area encounter table.</summary>
public sealed record BattleEnemyRosterSource(int Formation, int SceneSlot, string FormationHash)
{
    public void Validate()
    {
        if (Formation < 0 || SceneSlot is < 0 or >= NativeBattleSlots.FormationSlotCount || FormationHash is null || FormationHash.Length != 64 || !FormationHash.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Invalid added enemy placement provenance.");
        }
    }
}
