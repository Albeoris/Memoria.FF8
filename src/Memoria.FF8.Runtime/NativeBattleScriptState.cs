namespace Memoria.FF8.Runtime;

/// <summary>Allocation-independent battle script owners from487DF0, retained without replaying callbacks.</summary>
public sealed record NativeBattleScriptState
{
    public bool DisableAutomaticTargetReplacement { get; init; }
    public byte ResultFlags { get; init; }
    public ushort[] WaitCounters { get; init; } = [];
    public byte[] AcquiredGuardianForces { get; init; } = [];
    public byte[] Cards { get; init; } = [];
    public int[] UnlockedTutorials { get; init; } = [];

    public void Validate()
    {
        if (WaitCounters is null || WaitCounters.Length > 255 || AcquiredGuardianForces is null || AcquiredGuardianForces.Length > 255 || AcquiredGuardianForces.Any(id => id >= 16) || Cards is null || Cards.Length > 255 || Cards.Any(id => id >= NativeCardInventory.CardCount) || UnlockedTutorials is null || UnlockedTutorials.Any(id => id is < 0 or >= 128) || UnlockedTutorials.Distinct().Count() != UnlockedTutorials.Length)
        {
            throw new InvalidDataException("Native battle script state has invalid callback, acquisition or persistent flag owners.");
        }
    }

    public NativeBattleScriptState DeepCopy()
    {
        Validate();
        return this with { WaitCounters = WaitCounters.ToArray(), AcquiredGuardianForces = AcquiredGuardianForces.ToArray(), Cards = Cards.ToArray(), UnlockedTutorials = UnlockedTutorials.ToArray() };
    }
}
