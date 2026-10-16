using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private uint ReadNativeActorVariable(BattleCombatant actor, int variable)
    {
        (BattleCombatant owner, int member) = ResolveNativeActorVariable(actor, variable);
        if (member is >= 36 and <= 64)
        {
            return owner.AiVariables[(member - 36) / 4];
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(owner.MutableNativeStatusResistances.AsSpan(member - 144, 4));
    }

    private void WriteNativeActorVariable(BattleCombatant actor, int variable, uint operand, bool add)
    {
        (BattleCombatant owner, int member) = ResolveNativeActorVariable(actor, variable);
        if (member is >= 36 and <= 64)
        {
            int index = (member - 36) / 4;
            owner.AiVariables[index] = add ? unchecked(owner.AiVariables[index] + operand) : operand;
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
        int offset = checked(actor.NativeIndex * 208 + 4 * (variable - 211));
        if (offset < 0)
        {
            throw new NotSupportedException($"Native battle AI variable {variable} aliases memory before the retained actor bank.");
        }
        int index = offset / 208;
        int member = offset % 208;
        BattleCombatant? owner = combatants.SingleOrDefault(candidate => candidate.NativeIndex == index);
        bool owned = member is >= 36 and <= 64 || member is >= 144 and <= 180 && owner?.MutableNativeStatusResistances.Length == 40;
        if (index >= 7 || owner is null || !owned)
        {
            throw new NotSupportedException($"Native battle AI variable {variable} aliases unowned actor memory at slot{index}+{member}.");
        }
        RequireNativeAllocationStatistics(owner);
        return (owner, member);
    }
}
