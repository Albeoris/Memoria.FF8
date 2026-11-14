using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattlePresentationPartyRows
{
    internal static ImmutableArray<int> Resolve(int sourceIndex, byte? sourceModelId, bool? sourceIsParty, GuardianFormInvocationMode? guardianFormMode,
        IEnumerable<NativeBattleEffectModelBinding>? nativeTargets, IEnumerable<NativeBattleForeignActor>? foreignActors, int? expectedPartyCount)
    {
        if (foreignActors is null)
        {
            return [0, 1, 2];
        }
        if (guardianFormMode.HasValue && sourceIsParty == false)
        {
            throw new InvalidDataException("A hosted Guardian Form source must belong to the party.");
        }

        HashSet<int> occupied = [];
        List<int> party = [];
        Bind(sourceIndex, guardianFormMode.HasValue || IsParty(sourceIsParty, sourceModelId));
        foreach (NativeBattleEffectModelBinding target in nativeTargets ?? [])
        {
            Bind(target.Model.Index, IsParty(target.IsParty, target.Model.ModelId4));
        }
        foreach (NativeBattleForeignActor actor in foreignActors)
        {
            Bind(actor.Index, actor.IsParty);
        }
        if (expectedPartyCount is int expected && party.Count != expected)
        {
            throw new NotSupportedException("The native party presentation operation exceeds the host's available physical model bank.");
        }
        party.Sort();
        return party.ToImmutableArray();

        void Bind(int index, bool isParty)
        {
            if (!NativeBattleSlots.Contains(index) || !occupied.Add(index))
            {
                throw new InvalidDataException("An effect model row requires one original actor binding outside its source row.");
            }
            if (isParty)
            {
                party.Add(index);
            }
        }
    }

    private static bool IsParty(bool? explicitParty, byte? modelId)
    {
        return explicitParty ?? (modelId ?? throw new NotSupportedException("Native party presentation requires the bound actor's model identity or party ownership.")) < 16;
    }
}
