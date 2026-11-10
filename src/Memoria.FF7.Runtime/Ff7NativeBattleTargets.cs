using System.Numerics;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleTargetMasks(ushort Available, ushort Targetable, ushort KnockedOut, ushort CloseRange, ushort FacingGroup0, ushort FacingGroup2, ushort ExcludedKnockouts = 0);

/// <summary>Revalidates the physical target mask for an ordinary queued action.</summary>
public static class Ff7NativeBattleTargets
{
    private const int Party = 0x00F;
    private const int Enemies = 0x3F0;

    public static ushort ResolvePlayer(int sourceSlot, ushort queued, byte targetFlags, ushort specialFlags, bool groupCast, Ff7BattleTargetMasks masks, Ff7BattleRng random)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)sourceSlot, 3u);
        return Resolve(sourceSlot, queued, targetFlags, specialFlags, groupCast, masks, random);
    }

    public static ushort Resolve(int sourceSlot, ushort queued, byte targetFlags, ushort specialFlags, bool groupCast, Ff7BattleTargetMasks masks, Ff7BattleRng random)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)sourceSlot, 10u);
        ArgumentNullException.ThrowIfNull(random);
        if (targetFlags == 0)
        {
            return (ushort)(1 << sourceSlot);
        }
        if ((targetFlags & 0x40) != 0)
        {
            return (ushort)(masks.Targetable & ~masks.KnockedOut);
        }

        int eligible = masks.Available & masks.Targetable;
        if ((specialFlags & 0x900) != 0x900)
        {
            eligible |= masks.KnockedOut & ~masks.ExcludedKnockouts;
        }

        bool defaultToEnemies = ((targetFlags & 2) != 0) == (sourceSlot < 4);
        int defaultSide = defaultToEnemies ? Enemies : Party;
        if ((targetFlags & 0x10) != 0)
        {
            eligible &= defaultSide;
        }
        else if ((targetFlags & 0x20) != 0)
        {
            eligible &= masks.CloseRange;
        }

        int targets = queued & eligible;
        int originalCount = BitOperations.PopCount(queued);
        bool selectedGroup = originalCount != 1;
        if ((targetFlags & 0x0C) == 4 || (targets != 0 && originalCount > 1))
        {
            int allowedGroup = (targets & Party) == 0 ? Enemies : Party;
            if ((targets & masks.FacingGroup0) == 0)
            {
                allowedGroup &= ~masks.FacingGroup0;
            }
            if ((targets & masks.FacingGroup2) == 0)
            {
                allowedGroup &= ~masks.FacingGroup2;
            }
            targets = eligible & allowedGroup;
        }

        if (targets == 0)
        {
            targets = eligible & defaultSide;
            if ((targetFlags & 0x0C) == 0x0C && !selectedGroup)
            {
                targets = Pick(targets, random);
            }
        }
        if ((targetFlags & 4) == 0 || ((targetFlags & 8) != 0 && sourceSlot < 3 && selectedGroup && !groupCast))
        {
            targets = Pick(targets, random);
        }
        if ((targets & masks.FacingGroup0) != 0 && (targets & masks.FacingGroup2) != 0)
        {
            targets &= (random.NextByte() & 2) == 0 ? masks.FacingGroup0 : masks.FacingGroup2;
        }
        return (ushort)targets;
    }

    private static int Pick(int mask, Ff7BattleRng random)
    {
        if (mask == 0)
        {
            return 0;
        }

        int ordinal = (random.NextByte() * BitOperations.PopCount((uint)mask)) >> 8;
        while (ordinal > 0)
        {
            mask &= mask - 1;
            ordinal--;
        }
        return mask & -mask;
    }
}
