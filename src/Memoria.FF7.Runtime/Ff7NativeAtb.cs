using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>Original FFVII battle gauge initialization and one eligible battle-time update.</summary>
public static class Ff7NativeAtb
{
    public const int SlotCount = 10;

    public static byte ResolveLayout(byte formationLayout, ushort formationFlags, bool preemptiveRequested)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(formationLayout, (byte)8);
        return formationLayout == 0 && (formationFlags & 0x10) != 0 && preemptiveRequested ? (byte)1 : formationLayout;
    }

    public static byte BattleTypeForLayout(byte layout)
    {
        return layout switch
        {
            <= 4 => layout,
            <= 7 => 3,
            8 => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(layout))
        };
    }

    public static ushort[] InitializeForLayout(ushort activeSlots, byte layout, bool forcePartyReady, Ff7BattleRng random)
    {
        return Initialize(activeSlots, BattleTypeForLayout(layout), forcePartyReady, random);
    }

    public static Ff7BattleAiActor[] ApplyFormationConditions(byte layout, IReadOnlyList<Ff7BattleAiActor> actors, SceneBattleData formation)
    {
        ArgumentNullException.ThrowIfNull(formation);
        Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(actors, 0);
        byte battleType = BattleTypeForLayout(layout);
        int party = masks.Allocated & 7;
        int enemies = masks.Allocated & 0x3F0;
        int facing = 0;
        int exposed = 0;
        switch (battleType)
        {
            case 0 or 1:
                exposed = enemies;
                break;
            case 2:
                exposed = party;
                break;
            case 4:
                foreach (Ff7BattleAiActor actor in actors)
                {
                    if (actor.Slot >= 4 && (actor.Conditions & 2) != 0)
                    {
                        facing |= 1 << actor.Slot;
                    }
                }
                exposed = facing | (party & 2);
                if (formation.FormationId == 0x3D6)
                {
                    exposed &= ~party;
                }
                break;
            default:
                facing = party & (layout == 7 ? 5 : ~5);
                exposed = facing;
                foreach (SceneBattleEnemy enemy in formation.Enemies)
                {
                    if (enemy.Placement.Position.Z >= 0)
                    {
                        exposed |= 1 << (enemy.FormationSlot + 4);
                    }
                }
                break;
        }

        Ff7BattleAiActor[] result = new Ff7BattleAiActor[actors.Count];
        for (int index = 0; index < result.Length; index++)
        {
            Ff7BattleAiActor actor = actors[index];
            int bit = 1 << actor.Slot;
            uint conditions = actor.Conditions & ~0x82U;
            if ((facing & bit) != 0)
            {
                conditions |= 2;
            }
            if ((exposed & bit) != 0)
            {
                conditions |= 0x80;
            }
            if (actor.Slot < 3 && battleType >= 2)
            {
                conditions = battleType == 2 ? conditions ^ 0x40 : conditions & ~0x40U;
            }
            result[index] = actor with { Conditions = conditions };
        }
        return result;
    }

    public static ushort[] Initialize(ushort activeSlots, byte battleType, bool forcePartyReady, Ff7BattleRng random)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(activeSlots, (ushort)0x3FF);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(battleType, (byte)5);

        ushort[] gauges = new ushort[SlotCount];
        int greatest = 0;
        for (int slot = 0; slot < gauges.Length; slot++)
        {
            if ((activeSlots & (1 << slot)) != 0)
            {
                gauges[slot] = (ushort)(random.NextWord() >> 1);
                greatest = Math.Max(greatest, gauges[slot]);
            }
        }
        for (int slot = 0; slot < gauges.Length; slot++)
        {
            if ((activeSlots & (1 << slot)) == 0)
            {
                continue;
            }
            int initial = battleType switch
            {
                0 or 5 => 0xE000 - greatest + gauges[slot],
                2 or 4 => slot < 4 ? 0 : 0xF000 - greatest + gauges[slot],
                _ => slot < 4 ? 0xFFFE : gauges[slot] >> 3
            };
            gauges[slot] = (ushort)(forcePartyReady ? slot < 3 ? 0xFFFE : 0 : initial);
        }
        return gauges;
    }

    public static int SpeedDenominator(ReadOnlySpan<byte> partyDexterity)
    {
        if (partyDexterity.Length is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(partyDexterity));
        }
        int sum = 0;
        foreach (byte dexterity in partyDexterity)
        {
            sum += dexterity;
        }
        return 50 + (sum + partyDexterity.Length - 1) / partyDexterity.Length;
    }

    public static int BaseRate(byte battleSpeed) => 65536 / (2 * (120 + battleSpeed * 480 / 256));

    public static ushort Increment(int slot, byte dexterity, sbyte dexterityModifier, uint status, int denominator, byte battleSpeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(denominator, 50);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(denominator, 305);
        ArgumentOutOfRangeException.ThrowIfLessThan(dexterityModifier, (sbyte)-100);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dexterityModifier, (sbyte)100);
        if ((status & 0x02004405) != 0)
        {
            return 0;
        }
        int rate = BaseRate(battleSpeed);
        if ((status & 0x200) == 0)
        {
            rate *= 2;
            if ((status & 0x100) != 0)
            {
                rate *= 2;
            }
        }
        int speed = slot < 3 ? dexterity + dexterity * dexterityModifier / 100 + 50 : dexterity;
        return (ushort)(rate * speed / denominator);
    }

    public static bool Advance(ref ushort gauge, ushort increment, bool fast, bool frozen)
    {
        if (gauge == ushort.MaxValue || frozen)
        {
            return false;
        }
        gauge = (ushort)Math.Min(ushort.MaxValue, gauge + (fast ? increment * 4 : increment));
        return gauge == ushort.MaxValue;
    }
}
