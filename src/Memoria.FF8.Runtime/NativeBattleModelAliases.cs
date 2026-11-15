using System.Buffers.Binary;
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

    internal ImmutableArray<byte>? EncodeOperand(BattleModelOwner owner, int width)
    {
        if (width is not (1 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A native model operand occupies one, two or four bytes.");
        }
        RequireBound(owner);
        if (!TryEncodeExact(owner, out byte operand))
        {
            return null;
        }
        ImmutableArray<byte> bytes = width switch
        {
            1 => [operand],
            2 => [operand, 0],
            _ => [operand, 0, 0, 0]
        };
        return bytes;
    }

    internal ImmutableArray<byte>? EncodeAddress(BattleModelOwner owner)
    {
        RequireBound(owner);
        if (!TryEncodeExact(owner, out byte operand))
        {
            return null;
        }
        uint address = NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(operand));
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, address);
        ImmutableArray<byte> encoded = [.. bytes];
        return encoded;
    }

    private void RequireBound(BattleModelOwner owner)
    {
        if (owner is null || !Roster.Contains(owner))
        {
            throw new InvalidDataException("A native model encoding requires an owner bound to this writer's roster.");
        }
    }

    internal ushort? EncodeTargetMask(BattleModelTargets targets)
    {
        ushort mask = 0;
        foreach (BattleModelOwner target in targets.Enumerate())
        {
            if (!TryEncodeExact(target, out byte operand))
            {
                return null;
            }
            mask |= (ushort)(1 << operand);
        }
        return mask;
    }

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
