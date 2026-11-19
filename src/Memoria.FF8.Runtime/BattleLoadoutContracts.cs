namespace Memoria.FF8.Runtime;

/// <summary>Stable assignment identities; subactions and learned progress remain owned by their native family.</summary>
public enum BattleLimitFamily
{
    Renzokuken,
    FireCross,
    Desperado,
    BloodPain,
    MassiveAnchor,
    Shot,
    Combine,
    AngelWing,
    IceStrike,
    Slot,
    BlueMagic,
    Duel
}

[Flags]
public enum BattleWeaponCapabilities
{
    None = 0,
    Gunblade = 1,
    MachineGun = 2,
    PairedBlades = 4,
    ThrowingSpear = 8,
    ShotFirearm = 16,
    Unarmed = 32
}

/// <summary>Equipment mechanics and resource provenance independent of the receiving body.</summary>
public sealed record BattleWeaponProfile(int WeaponId, int ResourceOwner, int Tier, BattleWeaponCapabilities Capabilities, byte RenzokukenFinisherMask)
{
    public bool Supports(BattleWeaponCapabilities capability)
    {
        return (Capabilities & capability) == capability;
    }
}

public static class BattleLimitFamilyRules
{
    public static bool HasAssignment(BattleLoadoutOverrides? loadouts, int characterId, BattleLimitFamily family)
    {
        if (characterId is < 0 or > 10)
        {
            return false;
        }
        return (loadouts?.Get(characterId)?.LimitFamilies ?? NativeFamilies(characterId)).Contains(family);
    }

    public static int SourceCharacter(BattleLimitFamily family)
    {
        return family switch
        {
            BattleLimitFamily.Renzokuken => 0,
            BattleLimitFamily.Duel => 1,
            BattleLimitFamily.Shot => 2,
            BattleLimitFamily.BlueMagic => 3,
            BattleLimitFamily.Combine or BattleLimitFamily.AngelWing => 4,
            BattleLimitFamily.Slot => 5,
            BattleLimitFamily.FireCross => 6,
            BattleLimitFamily.IceStrike => 7,
            BattleLimitFamily.Desperado => 8,
            BattleLimitFamily.BloodPain => 9,
            BattleLimitFamily.MassiveAnchor => 10,
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
    }

    public static BattleWeaponCapabilities RequiredWeapon(BattleLimitFamily family)
    {
        return family switch
        {
            BattleLimitFamily.Renzokuken or BattleLimitFamily.FireCross => BattleWeaponCapabilities.Gunblade,
            BattleLimitFamily.Desperado => BattleWeaponCapabilities.MachineGun,
            BattleLimitFamily.BloodPain => BattleWeaponCapabilities.PairedBlades,
            BattleLimitFamily.MassiveAnchor => BattleWeaponCapabilities.ThrowingSpear,
            BattleLimitFamily.Shot => BattleWeaponCapabilities.ShotFirearm,
            BattleLimitFamily.Combine or BattleLimitFamily.AngelWing or BattleLimitFamily.IceStrike or BattleLimitFamily.Slot or BattleLimitFamily.BlueMagic or BattleLimitFamily.Duel => BattleWeaponCapabilities.None,
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
    }

    public static BattleLimitFamily[] NativeFamilies(int characterId)
    {
        return characterId switch
        {
            0 => [BattleLimitFamily.Renzokuken],
            1 => [BattleLimitFamily.Duel],
            2 => [BattleLimitFamily.Shot],
            3 => [BattleLimitFamily.BlueMagic],
            4 => [BattleLimitFamily.Combine, BattleLimitFamily.AngelWing],
            5 => [BattleLimitFamily.Slot],
            6 => [BattleLimitFamily.FireCross],
            7 => [BattleLimitFamily.IceStrike],
            8 => [BattleLimitFamily.Desperado],
            9 => [BattleLimitFamily.BloodPain],
            10 => [BattleLimitFamily.MassiveAnchor],
            _ => throw new ArgumentOutOfRangeException(nameof(characterId))
        };
    }
}
