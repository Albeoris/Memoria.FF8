namespace Memoria.FF8.Runtime;

/// <summary>Recreation gameplay choices owned by one standalone save, separate from native FF8 configuration.</summary>
public sealed record GameplayPreferences
{
    public BattleAtbMode AtbMode { get; init; } = BattleAtbMode.TurnBased;
    public BattleGunbladeMode GunbladeMode { get; init; } = BattleGunbladeMode.Automatic;

    public bool SharedMagicStock { get; init; }
    public bool InfiniteMagic { get; init; }
    public bool SmartAttack { get; init; }

    public void Validate()
    {
        if (!Enum.IsDefined(AtbMode) || !Enum.IsDefined(GunbladeMode))
        {
            throw new InvalidDataException("Invalid saved gameplay preferences.");
        }
    }
}
