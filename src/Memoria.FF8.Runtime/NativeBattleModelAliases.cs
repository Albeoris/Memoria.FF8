using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The original operand bindings available to a native packet writer.</summary>
public sealed class NativeBattleModelAliases
{
    private readonly ImmutableDictionary<NativeBattleModelSlot, BattleModelOwner> ownersBySlot;

    public NativeBattleModelAliases(BattleModelRoster roster, IEnumerable<(NativeBattleModelSlot Slot, BattleModelOwner Owner)> mappings)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(mappings);
        var slots = ImmutableDictionary.CreateBuilder<NativeBattleModelSlot, BattleModelOwner>();
        HashSet<BattleModelOwner> owners = [];
        foreach ((NativeBattleModelSlot slot, BattleModelOwner owner) in mappings)
        {
            if (owner is null || owner.Side != slot.Side || !roster.Contains(owner) || slots.ContainsKey(slot) || !owners.Add(owner))
            {
                throw new ArgumentException("An original model alias requires a unique slot and matching roster owner.", nameof(mappings));
            }
            slots.Add(slot, owner);
        }
        Roster = roster;
        ownersBySlot = slots.ToImmutable();
    }

    internal BattleModelRoster Roster { get; }

    internal bool TryEncodeExact(BattleModelOwner owner, out byte operand)
    {
        ArgumentNullException.ThrowIfNull(owner);
        foreach ((NativeBattleModelSlot slot, BattleModelOwner current) in ownersBySlot)
        {
            if (current == owner)
            {
                operand = slot.Offset;
                return true;
            }
        }
        operand = default;
        return false;
    }

    internal BattleModelOwner DecodeLiteral(byte operand)
    {
        NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(operand);
        if (!ownersBySlot.TryGetValue(slot, out BattleModelOwner? owner))
        {
            throw new InvalidDataException("The native operand has no original model alias.");
        }
        return owner;
    }
}
