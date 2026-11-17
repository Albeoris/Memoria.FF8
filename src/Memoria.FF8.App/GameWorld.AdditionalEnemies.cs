using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal static IEnumerable<BattleEnemyPlacement> AuthoredEnemyPlacements(BattleEncounter encounter)
    {
        return encounter.Slots.Where(slot => slot.NativeModelId != 255 && (slot.Enabled || slot.NativeModelId != 0));
    }

    private static bool IsEnemyPrototype(BattleEnemyPlacement slot)
    {
        return slot.IsEnemyModel && slot.NativeModelId != 255;
    }

    internal static void RequireEnemyStatisticsContract(BattleEnemyPlacement placement)
    {
        if (!placement.IsEnemyModel)
        {
            throw new NotSupportedException($"Scene slot {placement.Slot} requires the native character participant statistics contract for model {placement.NativeModelId}.");
        }
    }

    private static bool IsOrdinaryEnemyPlacement(BattleEnemyPlacement slot)
    {
        return IsEnemyPrototype(slot) && slot.Enabled && !slot.Hidden && !slot.Untargetable && !slot.InitiallySuspended;
    }

    internal static (HashSet<int> Slots, BattleEncounterRosterAdmission Admission) InspectEncounterRoster(BattleEncounter encounter, Func<int, NativeBattleAiDefinition> readEnemyAi, IReadOnlyList<BattleNativeAction> actions)
    {
        HashSet<BattleEncounterRosterRestrictionReason> restrictions = [];
        HashSet<int> slots = [];
        HashSet<int> visitedModels = [];
        Queue<BattleEnemyPlacement> pending = new(encounter.Enemies.Where(slot => slot.Enabled && IsEnemyPrototype(slot)));
        while (pending.TryDequeue(out BattleEnemyPlacement? placement))
        {
            if (!visitedModels.Add(placement.ModelId))
            {
                continue;
            }
            NativeBattleAiDefinition program = readEnemyAi(placement.ModelId);
            HashSet<int> referenced = [];
            for (int band = 0; band < 3; band++)
            {
                NativeBattleExpandedAi.Evaluate(program with { LevelBand = band }, actions, encounter.Id, originalEnemySlots: true, reserveSceneSlots: referenced,
                    rosterModel: placement.ModelId, rosterRestrictions: restrictions);
            }
            foreach (int slot in referenced.Where(slots.Add))
            {
                if ((uint)slot >= ExpandedBattleFormation.OriginalEnemyPrototypeCount)
                {
                    throw new InvalidDataException($"Formation {encounter.Id} model {placement.NativeModelId} references reserve slot {slot} outside the original formation slots.");
                }
                BattleEnemyPlacement prototype = encounter.Slots.SingleOrDefault(candidate => candidate.Slot == slot)
                    ?? throw new InvalidDataException($"Formation {encounter.Id} model {placement.NativeModelId} references missing reserve slot {slot}.");
                if (prototype.NativeModelId == 255)
                {
                    throw new InvalidDataException($"Formation {encounter.Id} model {placement.NativeModelId} references unallocated reserve slot {slot}.");
                }
                RequireEnemyStatisticsContract(prototype);
                pending.Enqueue(prototype);
            }
        }
        return (slots, BattleEncounterRosterAdmission.ForFormation(encounter.Id, restrictions));
    }

    private static BattleEnemyPlacement[] OriginalExpandedEnemyPlacements(BattleEncounter encounter, BattleDefinition? restoredDefinition, HashSet<int> referenced)
    {
        BattleEnemyPlacement[] required = encounter.Slots.Where(slot => slot.Enabled || referenced.Contains(slot.Slot)).ToArray();
        if (restoredDefinition is null)
        {
            return required;
        }
        HashSet<int> retained = restoredDefinition.Combatants.Where(actor => !actor.IsPlayer && actor.SceneSlot.HasValue &&
            restoredDefinition.AddedEnemySources?.ContainsKey(actor.SceneSlot.Value) != true).Select(actor => actor.SceneSlot!.Value).ToHashSet();
        BattleEnemyPlacement[] placements = encounter.Slots.Where(slot => retained.Contains(slot.Slot)).ToArray();
        if (placements.Length != retained.Count || required.Any(slot => !retained.Contains(slot.Slot)))
        {
            throw new InvalidDataException($"Saved formation {encounter.Id} does not contain its required original enemy scene slots.");
        }
        return placements;
    }

    // The original authored model operand bank puts the enemy aliases after the three party aliases.
    private const int FirstEnemyModelOperand = 3;
    private const int OriginalEnemyAliasCount = ExpandedBattleFormation.MaximumEnemies - ExpandedBattleFormation.MaximumAddedEnemies;

    private sealed record BattleDonorGroup(BattleEnemyPlacement Root, ImmutableArray<BattleEnemyPlacement> Members);

    private readonly Dictionary<int, BattleDonorGroup[]> donorGroups = [];
    private BattleAssets? donorGroupAssets;
    private int? donorGroupDestination;

    private BattleDonorGroup[] ReadEligibleDonorGroups(BattleEncounter source, int destination)
    {
        if (!ReferenceEquals(donorGroupAssets, BattleResources) || donorGroupDestination != destination)
        {
            donorGroups.Clear();
            donorGroupAssets = BattleResources;
            donorGroupDestination = destination;
        }
        if (!donorGroups.TryGetValue(source.Id, out BattleDonorGroup[]? groups))
        {
            groups = source.Enemies.Where(IsOrdinaryEnemyPlacement).Select(root => TryReadDonorGroup(source, root, destination)).OfType<BattleDonorGroup>().ToArray();
            donorGroups.Add(source.Id, groups);
        }
        return groups;
    }

    private BattleDonorGroup? TryReadDonorGroup(BattleEncounter source, BattleEnemyPlacement root, int destination)
    {
        try
        {
            return ReadDonorGroup(source, root, destination);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private BattleDonorGroup ReadDonorGroup(BattleEncounter source, BattleEnemyPlacement root, int destination)
    {
        BattleEnemyPlacement[] original = source.Enemies.OrderBy(placement => placement.Slot).Take(OriginalEnemyAliasCount).ToArray();
        var placements = source.Slots.ToDictionary(placement => placement.Slot);
        Dictionary<int, BattleEnemyPlacement> members = [];
        Queue<BattleEnemyPlacement> pending = new();
        HashSet<byte> unresolvedOperands = [];
        HashSet<int> reservedSlots = [];
        ImmutableArray<NativeBattleEffectBinding> effects = assets.ReadNativeEffectCatalog();
        Add(root);
        while (pending.TryDequeue(out BattleEnemyPlacement? actor))
        {
            HashSet<int> references = [];
            HashSet<int> reserves = [];
            HashSet<byte> operands = [];
            HashSet<ushort> reachedEffects = [];
            HashSet<BattleEncounterRosterRestrictionReason> restrictions = [];
            NativeBattleAiDefinition ai = BattleResources.ReadEnemyAi(actor.ModelId, 0);
            for (int band = 0; band < 3; band++)
            {
                ExpandedAiAdmission admission = NativeBattleExpandedAi.Evaluate(ai with { LevelBand = band }, BattleResources.NativeActions, destination, originalEnemySlots: true,
                    reserveSceneSlots: reserves, explicitEnemyModelReferences: references, rosterModel: actor.ModelId, rosterRestrictions: restrictions,
                    reachedEffects: reachedEffects, originalModelOperands: operands);
                if (admission != ExpandedAiAdmission.Supported)
                {
                    throw new NotSupportedException($"Source formation {source.Id}, model {actor.NativeModelId} requires {admission}.");
                }
            }
            if (restrictions.Count != 0)
            {
                throw new NotSupportedException($"Source formation {source.Id}, model {actor.NativeModelId} requires authored party membership.");
            }

            foreach (int reserve in reserves)
            {
                if (!placements.TryGetValue(reserve, out BattleEnemyPlacement? prototype) || !IsEnemyPrototype(prototype))
                {
                    throw new NotSupportedException($"Source formation {source.Id} has no installed reserve for scene slot {reserve}.");
                }
                reservedSlots.Add(reserve);
                Add(prototype);
            }
            foreach (int model in references)
            {
                BattleEnemyPlacement[] matches = original.Where(candidate => candidate.NativeModelId == model).ToArray();
                if (matches.Length == 0 && !placements.Values.Any(candidate => reservedSlots.Contains(candidate.Slot) && candidate.NativeModelId == model))
                {
                    throw new NotSupportedException($"Source formation {source.Id} has no member for AI model operand {model}.");
                }
                foreach (BattleEnemyPlacement matched in matches)
                {
                    Add(matched);
                }
            }
            foreach (byte operand in operands)
            {
                AddOperand(operand);
            }
            BattleModel modelResource = BattleResources.LoadEnemy(actor.ModelId);
            BattleSequenceSet programs = modelResource.Sequences ?? throw new NotSupportedException($"Model {actor.NativeModelId} has no original sequence resource.");
            foreach (NativeBattleSequenceReference reference in programs.ReadModelReferences())
            {
                if (reference.Kind == NativeBattleSequenceReferenceKind.OriginalSlot)
                {
                    AddOperand(reference.Operand);
                    continue;
                }
                IEnumerable<BattleEnemyPlacement> matches = original.Where(candidate => candidate.NativeModelId == reference.Operand);
                if (reference.Kind == NativeBattleSequenceReferenceKind.FirstMatchingModel)
                {
                    matches = matches.Take(1);
                }
                foreach (BattleEnemyPlacement matched in matches)
                {
                    Add(matched);
                }
            }
            foreach (ushort effect in reachedEffects.Where(effect => effect != 0))
            {
                int effectIndex = effect - 1;
                if (effectIndex >= effects.Length || effects.IsEmpty)
                {
                    throw new NotSupportedException($"Source formation {source.Id} requests absent native effect {effect}.");
                }
                NativeBattleEffectBinding binding = effects[effectIndex].Entry == 0 ? effects[0] : effects[effectIndex];
                if (binding.Entry == 0)
                {
                    throw new NotSupportedException($"Source formation {source.Id} cannot bind native effect {effect}.");
                }
                foreach (byte operand in binding.AuthoredModelOperands)
                {
                    AddOperand(operand);
                }
                int actorOrdinal = Array.FindIndex(original, member => member.Slot == actor.Slot);
                if (actorOrdinal < 0 && !binding.CasterRelativeModels.IsEmpty)
                {
                    throw new NotSupportedException($"Source formation {source.Id} requires a proved allocated slot for effect {effect}.");
                }
                foreach (byte relative in binding.CasterRelativeModels)
                {
                    AddOperand(checked((byte)(FirstEnemyModelOperand + actorOrdinal + relative)));
                }
            }
        }
        if (unresolvedOperands.Count != 0 && reservedSlots.Count == 0)
        {
            throw new NotSupportedException($"Source formation {source.Id} has no live or allocated model for original operand {unresolvedOperands.Min()}.");
        }
        return new(root, [root, .. members.Values.Where(actor => actor.Slot != root.Slot).OrderBy(actor => actor.Slot)]);

        void Add(BattleEnemyPlacement placement)
        {
            RequireEnemyStatisticsContract(placement);
            if (members.TryAdd(placement.Slot, placement))
            {
                pending.Enqueue(placement);
            }
        }

        void AddOperand(byte operand)
        {
            int index = operand - FirstEnemyModelOperand;
            if (index < 0)
            {
                return;
            }
            if (index >= OriginalEnemyAliasCount)
            {
                throw new NotSupportedException($"Source formation {source.Id} references original model operand {operand} outside the available aliases.");
            }
            if (index >= original.Length)
            {
                unresolvedOperands.Add(operand);
                return;
            }
            Add(original[index]);
        }
    }

    private void AddDonorGroup(ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Builder sources, BattleEncounter source, BattleDonorGroup group)
    {
        int first = ExpandedBattleFormation.OriginalEnemyPrototypeCount + sources.Count;
        BattleSourceGroupId identity = new(first);
        string hash = BattleResources.FormationSourceHash(source.Id);
        foreach (BattleEnemyPlacement actor in group.Members)
        {
            sources.Add(ExpandedBattleFormation.OriginalEnemyPrototypeCount + sources.Count, new(source.Id, actor.Slot, hash)
            {
                Group = identity,
                OriginalEnemyOrdinal = ReadDonorOriginalOrdinal(source, actor)
            });
        }
    }

    private ImmutableArray<ushort>? ReadLocalEnemyFormations()
    {
        if (session.State.Scene == SceneKind.Field && Field is { } field && field.Id == session.State.FieldId)
        {
            return field.RandomEncounters is { Rate: > 0 } encounters ? encounters.Formations : [];
        }
        if (session.State.Scene == SceneKind.WorldMap && (worldScene?.State ?? session.State.World) is { GroundType: byte ground } world && session.State.DiscProcess is { } disc)
        {
            if (worldScene is { } scene)
            {
                return scene.ReadRandomEncounterDefinition(world)?.Formations ?? [];
            }
            // Restoring the frozen field can validate its battle before the world scene is allocated.
            using WorldAssets worldAssets = new(assets.GameDirectory, resources);
            return worldAssets.ReadRandomEncounterDefinition(world.X, world.Z, ground, disc.SavedDisc204)?.Formations ?? [];
        }
        return null;
    }

    private List<(BattleEncounter Source, BattleDonorGroup[] Groups)> BuildAdditionalEnemyDonorPool(BattleEncounter encounter)
    {
        List<(BattleEncounter, BattleDonorGroup[])> pool = [];
        foreach (int formation in (ReadLocalEnemyFormations() ?? []).Distinct())
        {
            if (formation >= BattleResources.FormationCount)
            {
                continue;
            }
            BattleEncounter candidate = BattleResources.LoadEncounter(formation);
            BattleDonorGroup[] groups = ReadEligibleDonorGroups(candidate, encounter.Id);
            if (groups.Length != 0)
            {
                pool.Add((candidate, groups));
            }
        }
        if (pool.Count == 0)
        {
            BattleDonorGroup[] groups = ReadEligibleDonorGroups(encounter, encounter.Id);
            if (groups.Length != 0)
            {
                pool.Add((encounter, groups));
            }
        }
        return pool;
    }

    private ImmutableSortedDictionary<int, BattleEnemyRosterSource> SelectAdditionalEnemies(BattleEncounter encounter, int count, byte seed, int originalReserveCount)
    {
        int available = Math.Min(count, ExpandedBattleFormation.MaximumEnemies - encounter.Enemies.Count - originalReserveCount);
        var sources = ImmutableSortedDictionary.CreateBuilder<int, BattleEnemyRosterSource>();
        if (available <= 0)
        {
            return sources.ToImmutable();
        }
        // Selection is replayable from the entry seed and does not spend battle or exploration RNG.
        NativeBattleRandomState random = NativeBattleRandomState.ForecastPhase(seed);
        List<(BattleEncounter Source, BattleDonorGroup[] Groups)> pool = BuildAdditionalEnemyDonorPool(encounter);
        while (pool.Count != 0 && sources.Count < available)
        {
            int previousCount = sources.Count;
            List<(BattleEncounter Source, BattleDonorGroup[] Groups)> round = [.. pool];
            while (round.Count != 0 && sources.Count < available)
            {
                int formationIndex = DrawIndex(round.Count);
                (BattleEncounter source, BattleDonorGroup[] groups) = round[formationIndex];
                round.RemoveAt(formationIndex);
                List<BattleDonorGroup> remaining = [.. groups];
                while (remaining.Count != 0 && sources.Count < available)
                {
                    int groupIndex = DrawIndex(remaining.Count);
                    BattleDonorGroup group = remaining[groupIndex];
                    remaining.RemoveAt(groupIndex);
                    if (group.Members.Length <= available - sources.Count)
                    {
                        AddDonorGroup(sources, source, group);
                    }
                }
            }
            if (sources.Count == previousCount)
            {
                break;
            }
        }
        return sources.ToImmutable();

        int DrawIndex(int length)
        {
            if (length == 1)
            {
                return 0;
            }
            int limit = 256 / length * length;
            byte roll;
            do
            {
                random = random.Draw(out roll);
            }
            while (roll >= limit);
            return roll % length;
        }
    }

    private BattleEnemyPlacement[] ResolveExpandedEnemyPlacements(BattleEncounter encounter, ImmutableSortedDictionary<int, BattleEnemyRosterSource> addedEnemies,
        out ImmutableSortedDictionary<int, BattleEnemyRosterSource> boundSources, bool originalSlots = false, IReadOnlyList<BattleEnemyPlacement>? originalPlacements = null, bool diagnosticRoster = false)
    {
        List<BattleEnemyPlacement> placements = originalSlots ? [.. originalPlacements ?? encounter.Slots] : encounter.Enemies.Select((enemy, index) => enemy with { Slot = index }).ToList();
        if (originalSlots && (placements.Any(enemy => (uint)enemy.Slot >= ExpandedBattleFormation.OriginalEnemyPrototypeCount) ||
            placements.Select(enemy => enemy.Slot).Distinct().Count() != placements.Count))
        {
            throw new InvalidDataException("Original enemy scene slots must be unique formation prototype indices.");
        }
        int firstAddedSlot = originalSlots ? ExpandedBattleFormation.OriginalEnemyPrototypeCount : placements.Count;
        int slotLimit = originalSlots ? ExpandedBattleFormation.MaximumRewardSlots : ExpandedBattleFormation.MaximumEnemies;
        if (addedEnemies.Count > ExpandedBattleFormation.MaximumAddedEnemies || firstAddedSlot + addedEnemies.Count > slotLimit ||
            !addedEnemies.Keys.SequenceEqual(Enumerable.Range(firstAddedSlot, addedEnemies.Count)))
        {
            throw new InvalidDataException("The added enemy roster does not use the required contiguous scene slots.");
        }
        foreach (BattleEnemyRosterSource source in addedEnemies.Values)
        {
            if (source is null)
            {
                throw new InvalidDataException("The added enemy roster has no selected placement source.");
            }
            source.Validate();
            if (source.Formation >= BattleResources.FormationCount || !BattleResources.FormationSourceHash(source.Formation).Equals(source.FormationHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The added enemy roster disagrees with its installed source formation.");
            }
        }
        Dictionary<int, BattleEncounter> donors = addedEnemies.Count == 0
            ? []
            : diagnosticRoster
                ? addedEnemies.Values.Select(source => source.Formation).Distinct().ToDictionary(formation => formation, BattleResources.LoadEncounter)
                : BuildAdditionalEnemyDonorPool(encounter).ToDictionary(donor => donor.Source.Id, donor => donor.Source);
        var bound = addedEnemies.ToBuilder();
        foreach (var group in addedEnemies.GroupBy(pair => pair.Value.Group ?? new BattleSourceGroupId(pair.Key)))
        {
            var members = group.OrderBy(pair => pair.Key).ToArray();
            BattleEnemyRosterSource seed = members[0].Value;
            if (group.Key.Slot != members[0].Key || members.Any(pair => pair.Value.Formation != seed.Formation))
            {
                throw new InvalidDataException("An added enemy group must be owned by its first slot and one source formation.");
            }
            if (!donors.TryGetValue(seed.Formation, out BattleEncounter? formation))
            {
                throw new InvalidDataException($"Added enemy source formation {seed.Formation} is outside the suspended exploration encounter table's pool and its destination fallback.");
            }
            BattleDonorGroup resolved = ReadEligibleDonorGroups(formation, encounter.Id).SingleOrDefault(candidate => candidate.Root.Slot == seed.SceneSlot)
                ?? throw new NotSupportedException($"Added enemy source formation {seed.Formation}, slot {seed.SceneSlot} has no supported model bindings.");
            if (!members.Select(pair => pair.Value.SceneSlot).SequenceEqual(resolved.Members.Select(placement => placement.Slot)))
            {
                throw new InvalidDataException("The added enemy roster does not contain its complete authored source group in order.");
            }
            for (int index = 0; index < members.Length; index++)
            {
                (int slot, BattleEnemyRosterSource source) = members[index];
                BattleEnemyPlacement enemy = resolved.Members[index];
                int? originalOrdinal = ReadDonorOriginalOrdinal(formation, enemy);
                if (source.OriginalEnemyOrdinal is int supplied && supplied != originalOrdinal)
                {
                    throw new InvalidDataException("An added enemy source has a different original model ordinal from its installed formation.");
                }
                if (diagnosticRoster && enemy.Enabled && placements.Any(candidate => candidate.Enabled && candidate.ModelId == enemy.ModelId))
                {
                    throw new InvalidDataException("Diagnostic roster donors must have distinct models and exclude the required enemy's model.");
                }
                bound[slot] = source with { Group = group.Key, OriginalEnemyOrdinal = originalOrdinal };
                placements.Add(enemy with { Slot = slot });
            }
        }
        boundSources = bound.ToImmutable();
        return placements.ToArray();
    }

    private static int? ReadDonorOriginalOrdinal(BattleEncounter source, BattleEnemyPlacement actor)
    {
        int ordinal = 0;
        foreach (BattleEnemyPlacement placement in source.Enemies.OrderBy(placement => placement.Slot).Take(OriginalEnemyAliasCount))
        {
            if (placement.Slot == actor.Slot)
            {
                return ordinal;
            }
            ordinal++;
        }
        return null;
    }
}
