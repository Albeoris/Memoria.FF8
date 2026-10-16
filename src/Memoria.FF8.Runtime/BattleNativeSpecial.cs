using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeEnemySpecial(byte CommonCard, byte RareCard, ImmutableArray<byte> DevourEffects, byte DevourOutcome);
public sealed record BattleNativeDevourEffect(byte Effect, byte Quantity, ushort SavedStatuses, uint BattleStatuses, byte StatFlags, byte HitPointBonus, string? Description);
public sealed record BattleNativeSpecialContext(byte? Card, int? DevourEffect, byte? DevourOutcome, bool Failed);
public sealed record BattleNativeCharacterGrowth(ushort HitPointBonus, ImmutableArray<byte> StatBonuses)
{
    public void Validate()
    {
        if (HitPointBonus > 9999 || StatBonuses.IsDefault || StatBonuses.Length != 6)
        {
            throw new InvalidDataException("Native character growth requires its saved HP WORD and six statistic bonus BYTEs.");
        }
    }
}
public sealed record BattleNativeGrowthChange(byte Row, BattleNativeCharacterGrowth Before, BattleNativeCharacterGrowth After);
public sealed record BattleNativeCardChange(NativeCardInventory Before, NativeCardInventory After);
