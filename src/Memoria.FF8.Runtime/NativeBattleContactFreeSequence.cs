using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleContactFreeSequence(ImmutableArray<byte> Program, byte Sequence, ImmutableHashSet<int> RetiredOffsets)
{
    internal static NativeBattleContactFreeSequence? Read(NativeBattleGraphicsResource resource, byte sequence)
    {
        if (sequence == 0 || !resource.Contains(sequence))
        {
            return null;
        }
        int start = resource.SequenceStart(sequence);
        int end = resource.SequenceEnd(sequence);
        Dictionary<int, (byte Opcode, int Next)> instructions = [];
        for (int position = start; position < end;)
        {
            int origin = position;
            byte opcode = NativeBattleSequenceEncoding.ReadOpcode(resource.SequenceSection, ref position);
            if (!(opcode < 0x80 || opcode is 0x91 or 0x9D or 0xA3 or 0xA9 or >= 0xC0 and <= 0xF3))
            {
                return null;
            }
            int next = NativeBattleSequenceEncoding.NextOffset(resource, opcode, origin, position);
            if (next > end)
            {
                return null;
            }
            if (opcode == 0xE5 && resource.SequenceSection[position] is >= 8 and < 120)
            {
                return null;
            }
            instructions.Add(origin, (opcode, next));
            position = next;
        }
        HashSet<(int Offset, bool Retired)> visited = [];
        Stack<(int Offset, bool Retired)> pending = new();
        ImmutableHashSet<int>.Builder retired = ImmutableHashSet.CreateBuilder<int>();
        HashSet<int> unretired = [];
        pending.Push((start, false));
        while (pending.TryPop(out var current))
        {
            if (!instructions.TryGetValue(current.Offset, out var instruction))
            {
                return null;
            }
            if (!visited.Add(current))
            {
                continue;
            }
            if (current.Retired)
            {
                retired.Add(current.Offset - start);
            }
            else
            {
                unretired.Add(current.Offset - start);
            }
            bool afterRetirement = current.Retired || instruction.Opcode == 0xA3;
            if (NativeBattleScriptExpressions.IsBranch(instruction.Opcode))
            {
                int target = checked(current.Offset + NativeBattleScriptExpressions.ReadRelativeOffset(instruction.Opcode, resource.SequenceSection.AsSpan()[(current.Offset + 1)..instruction.Next]));
                pending.Push((target, afterRetirement));
                if (NativeBattleScriptExpressions.IsUnconditionalBranch(instruction.Opcode))
                {
                    continue;
                }
            }
            pending.Push((instruction.Next, afterRetirement));
        }
        retired.ExceptWith(unretired);
        return retired.Count == 0 ? null : new(resource.SequenceSection, sequence, retired.ToImmutable());
    }

    internal bool Matches(NativeBattleGraphicsResource resource, NativeBattleGraphicsModel model)
    {
        return model.ReadLinkedModel() is null && MatchesControl(resource, model);
    }

    internal bool MatchesControl(NativeBattleGraphicsResource resource, NativeBattleGraphicsModel model)
    {
        return Program.Equals(resource.SequenceSection) && model.ModelId4 is not (null or 0x8F) && (model.Flags0 & 2) != 0 && model.Sequence == Sequence &&
            model.SequenceState is { Pending3: 0, Alternate43: 0 } state && state.Default2 == Sequence &&
            model.ResourceFlags44 is ushort flags && (flags & 0x40) != 0 && RetiredOffsets.Contains(state.Offset46);
    }
}

internal sealed record NativeBattleContactFreeRing(BattleModelOwner Source, ImmutableArray<NativeBattleContactFreeRing.Member> Members)
{
    internal sealed record Member(BattleModelOwner Owner, BattleModelOwner? Link, NativeBattleContactFreeSequence Sequence);

    internal static NativeBattleContactFreeRing? Read(BattleActorModels models, BattleModelOwner source, byte sequence, Func<BattleModelOwner, NativeBattleGraphicsResource> resource, NativeBattleContactFreeSequence? selectedSource = null)
    {
        if (models.Get(source).Flags8 is not uint sourceFlags || (sourceFlags & 0x18) != 0)
        {
            return null;
        }
        ImmutableArray<Member>.Builder members = ImmutableArray.CreateBuilder<Member>();
        foreach (BattleModelOwner owner in NativeBattleModelRings.Walk(models, source))
        {
            NativeBattleGraphicsModel model = models.Get(owner);
            if ((model.Flags0 & 2) == 0)
            {
                return null;
            }
            NativeBattleGraphicsResource original = resource(owner);
            NativeBattleContactFreeSequence? proof = owner == source && selectedSource is not null ? selectedSource : NativeBattleContactFreeSequence.Read(original, sequence);
            if (proof?.MatchesControl(original, model) != true)
            {
                return null;
            }
            members.Add(new(owner, model.ReadLinkedModel(), proof));
        }
        return members.Count == 0 ? null : new(source, members.ToImmutable());
    }

    internal bool Matches(BattleActorModels models, Func<BattleModelOwner, NativeBattleGraphicsResource> resource)
    {
        if (models.Get(Source).Flags8 is not uint sourceFlags || (sourceFlags & 0x18) != 0)
        {
            return false;
        }
        int index = 0;
        foreach (BattleModelOwner owner in NativeBattleModelRings.Walk(models, Source))
        {
            if (index >= Members.Length || Members[index].Owner != owner)
            {
                return false;
            }
            Member member = Members[index++];
            NativeBattleGraphicsModel model = models.Get(owner);
            if (model.ReadLinkedModel() != member.Link || !member.Sequence.MatchesControl(resource(owner), model))
            {
                return false;
            }
        }
        return index == Members.Length;
    }
}
