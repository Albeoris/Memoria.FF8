using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed class BattleSourceGrouping
{
    private readonly ImmutableDictionary<BattleEnemyActorModel, BattleSourceGroupId> groupsByOwner;
    private readonly ImmutableDictionary<BattleSourceGroupId, ImmutableArray<BattleSourceMember>> membersByGroup;

    public BattleSourceGrouping(BattleDefinition definition)
    {
        var members = ImmutableDictionary.CreateBuilder<BattleSourceGroupId, ImmutableArray<BattleSourceMember>>();
        var groups = ImmutableDictionary.CreateBuilder<BattleEnemyActorModel, BattleSourceGroupId>();
        if (definition.AddedEnemySources is { Count: > 0 } sources)
        {
            var actors = definition.Combatants.Where(actor => !actor.IsPlayer && actor.SceneSlot.HasValue).ToDictionary(actor => actor.SceneSlot!.Value);
            foreach (var group in sources.GroupBy(pair => pair.Value.GroupForSlot(pair.Key)))
            {
                var placements = group.OrderBy(pair => pair.Value.SceneSlot).ToArray();
                if (placements.Any(pair => pair.Value.Formation != placements[0].Value.Formation || !pair.Value.FormationHash.Equals(placements[0].Value.FormationHash, StringComparison.OrdinalIgnoreCase)) ||
                    placements.Select(pair => pair.Value.SceneSlot).Distinct().Count() != placements.Length)
                {
                    throw new InvalidDataException("A battle source group requires distinct placements from one installed formation.");
                }
                var entries = ImmutableArray.CreateBuilder<BattleSourceMember>(placements.Length);
                foreach ((int slot, BattleEnemyRosterSource source) in placements)
                {
                    if (!actors.TryGetValue(slot, out BattleCombatantDefinition? actor))
                    {
                        throw new InvalidDataException("A battle source placement has no admitted actor.");
                    }
                    BattleEnemyActorModel owner = new(new(actor.Id));
                    NativeBattleModelSlot? original = source.OriginalEnemyOrdinal is int ordinal ? NativeBattleModelSlot.Decode(checked((byte)(3 + ordinal))) : null;
                    entries.Add(new(owner, source.SceneSlot, checked((byte)(actor.ModelId + 16)), original));
                    groups.Add(owner, group.Key);
                }
                members.Add(group.Key, entries.MoveToImmutable());
            }
        }
        groupsByOwner = groups.ToImmutable();
        membersByGroup = members.ToImmutable();
    }

    public BattleSourceGroupId? FindGroup(BattleModelOwner owner) => owner is BattleEnemyActorModel enemy && groupsByOwner.TryGetValue(enemy, out BattleSourceGroupId group) ? group : null;

    public BattleSourceBindings Bind(BattleSourceGroupId group, BattleActorModels models, NativeBattleModelBoundary original, IEnumerable<BattleSourceMember> added)
    {
        ImmutableArray<BattleSourceMember> initial = membersByGroup.GetValueOrDefault(group, []);
        ImmutableArray<BattleSourceMember> members = initial.Concat(added).Where(member => models.Contains(member.Owner))
            .OrderBy(member => member.OriginalSlot?.Offset ?? byte.MaxValue).ThenBy(member => member.SceneSlot).ToImmutableArray();
        var aliases = new List<(NativeBattleModelSlot, BattleModelOwner)>();
        for (byte operand = 0; operand < 3; operand++)
        {
            NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(operand);
            if (original.FindOriginalOwner(slot, models) is { } party)
            {
                aliases.Add((slot, party));
            }
        }
        foreach (BattleSourceMember member in members)
        {
            if (member.OriginalSlot is { } slot)
            {
                aliases.Add((slot, member.Owner));
            }
        }
        return new(members, new NativeBattleModelAliases(models.Roster, aliases));
    }
}

internal sealed record BattleSourceMember(BattleEnemyActorModel Owner, int SceneSlot, byte NativeModel, NativeBattleModelSlot? OriginalSlot);
internal sealed record BattleSourcePlacementBinding(BattleSourceGroupId Group, int SceneSlot, int? OriginalEnemyOrdinal);

/// <summary>Authored model operands for one admitted source group at its current roster generation.</summary>
internal sealed class BattleSourceBindings(ImmutableArray<BattleSourceMember> members, NativeBattleModelAliases aliases)
{
    private NativeBattleModelAliases? lastPhysical;
    private NativeBattleModelAliases? scoped;

    public NativeBattleModelAliases ApplyAuthoredOperands(NativeBattleModelAliases physical)
    {
        if (scoped is not { } bound || !ReferenceEquals(lastPhysical, physical))
        {
            bound = physical.WithAuthoredOperands(aliases);
            lastPhysical = physical;
            scoped = bound;
        }
        return bound;
    }

    public BattleModelOwner DecodeLiteral(byte operand) => aliases.DecodeLiteral(operand);

    public bool HasOriginalOperand(NativeBattleModelSlot slot) => aliases.FindOriginalOwner(slot) is not null;

    public bool TryGetOriginalOperand(BattleModelOwner owner, out byte operand) => aliases.TryEncodeExact(owner, out operand);

    public BattleModelOwner DecodeRelative(BattleModelOwner source, int offset)
    {
        if (!aliases.TryEncodeExact(source, out byte operand))
        {
            throw new InvalidDataException("The source actor has no authored original model operand for its relative reference.");
        }
        return DecodeLiteral(checked((byte)(operand + offset)));
    }

    public BattleEnemyActorReference? FindSceneActor(int sceneSlot)
    {
        foreach (BattleSourceMember member in members)
        {
            if (member.SceneSlot == sceneSlot)
            {
                return member.Owner.Actor;
            }
        }
        return null;
    }

    public IEnumerable<BattleEnemyActorModel> FindModels(int nativeModel)
    {
        foreach (BattleSourceMember member in members)
        {
            if (member.NativeModel == nativeModel && aliases.Roster.Contains(member.Owner))
            {
                yield return member.Owner;
            }
        }
    }

    public BattleEnemyActorModel? FindFirstModel(int nativeModel)
    {
        foreach (BattleSourceMember member in members)
        {
            if (member.NativeModel == nativeModel && aliases.Roster.Contains(member.Owner))
            {
                return member.Owner;
            }
        }
        return null;
    }
}
