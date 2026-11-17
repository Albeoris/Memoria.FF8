using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void StoreNativeAiLastAttacker(BattleCombatant actor, int variable, bool add)
    {
        byte? original = actor.LastNativeAction?.AttackerIndex;
        if (!add && variable is >= 220 and <= 227 && actor.LastAttackerId is string attackerId)
        {
            int index = variable - 220;
            actor.AiVariables[index] = 0;
            actor.AiActorReferences[index] = new(attackerId, original);
            return;
        }
        uint operand = original ?? throw new NotSupportedException("Native AI last attacker has no original numeric operand.");
        WriteNativeActorVariable(actor, variable, operand, add);
    }

    private uint ReadNativeActorVariable(BattleCombatant actor, int variable)
    {
        (BattleCombatant owner, int member) = ResolveNativeActorVariable(actor, variable);
        if (member is >= 36 and <= 64)
        {
            int index = (member - 36) / 4;
            return ReadNativeAiLocalNumber(owner, index);
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(owner.MutableNativeStatusResistances.AsSpan(member - 144, 4));
    }

    private static uint ReadNativeAiLocalNumber(BattleCombatant owner, int index)
    {
        return owner.AiActorReferences.TryGetValue(index, out NativeBattleAiActorValue reference)
            ? reference.OriginalOperand ?? throw new NotSupportedException("Native AI actor reference has no original numeric operand.")
            : owner.AiVariables[index];
    }

    private void WriteNativeActorVariable(BattleCombatant actor, int variable, uint operand, bool add)
    {
        (BattleCombatant owner, int member) = ResolveNativeActorVariable(actor, variable);
        if (member is >= 36 and <= 64)
        {
            int index = (member - 36) / 4;
            uint local = add ? unchecked(ReadNativeAiLocalNumber(owner, index) + operand) : operand;
            owner.AiActorReferences.Remove(index);
            owner.AiVariables[index] = local;
            return;
        }
        Span<byte> destination = owner.MutableNativeStatusResistances.AsSpan(member - 144, 4);
        uint value = add ? unchecked(BinaryPrimitives.ReadUInt32LittleEndian(destination) + operand) : operand;
        BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
    }

    private (BattleCombatant Owner, int Member) ResolveNativeActorVariable(BattleCombatant actor, int variable)
    {
        //487DF0 indexes1D277C4 by52*actor+U8 variable; actor records begin1D27B10,211 DWORDs later.
        //48B310/48BBD0 own the forty resistance bytes at actor+144;48C5C0 deliberately retains them.
        if (variable is >= 220 and <= 227)
        {
            return (actor, 4 * (variable - 211));
        }
        int? sourceIndex = ReadScopedEnemySource(actor) is { } source ? source.OriginalEnemyOrdinal + NativeBattleSlots.AllyCount : actor.NativeActorIndex;
        if (sourceIndex is not int alias)
        {
            throw new NotSupportedException($"Native AI variable {variable} requires an actor-bank alias in its source group.");
        }
        int offset = checked(alias * 208 + 4 * (variable - 211));
        if (offset < 0)
        {
            throw new NotSupportedException($"Native battle AI variable {variable} aliases memory before the retained actor bank.");
        }
        int index = offset / 208;
        int member = offset % 208;
        BattleCombatant? owner = FindSourceOriginalActor(actor, index);
        bool owned = member is >= 36 and <= 64 || member is >= 144 and <= 180 && owner?.MutableNativeStatusResistances.Length == 40;
        if (owner is null || !owned)
        {
            throw new NotSupportedException($"Native battle AI variable {variable} aliases unowned actor memory at slot{index}+{member}.");
        }
        RequireNativeAllocationStatistics(owner);
        return (owner, member);
    }
}
