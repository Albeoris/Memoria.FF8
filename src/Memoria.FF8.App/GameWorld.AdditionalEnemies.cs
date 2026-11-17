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

    private ExpandedAiAdmission[] EnemyAdmissions(int model, int formation, ISet<int> explicitEnemyModels, ISet<BattleEncounterRosterRestrictionReason> rosterRestrictions)
    {
        NativeBattleAiDefinition program = BattleResources.ReadEnemyAi(model, 0);
        ImmutableArray<NativeBattleEffectBinding> effectCatalog = assets.ReadNativeEffectCatalog();
        return Enumerable.Range(0, 3).Select(band => NativeBattleExpandedAi.Evaluate(program with { LevelBand = band }, BattleResources.NativeActions, formation,
            explicitEnemyModelReferences: explicitEnemyModels, rosterModel: model, rosterRestrictions: rosterRestrictions,
            admitsDonorEffect: CanBindIndependentEffect)).ToArray();

        bool CanBindIndependentEffect(ushort effect)
        {
            if (effect == 0)
            {
                return true;
            }
            int index = effect - 1;
            if (index >= effectCatalog.Length)
            {
                return false;
            }
            NativeBattleEffectBinding selected = effectCatalog[index];
            if (selected.Entry == 0)
            {
                selected = effectCatalog[0];
            }
            return selected.Entry != 0 && !selected.RequiresOriginalCasterCompanion;
        }
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

    private BattleEnemyPlacement[] EligibleAdditionalEnemyPlacements(BattleEncounter candidate, int destinationFormation)
    {
        return candidate.Enemies.Where(enemy => IsEligibleAdditionalEnemyPlacement(enemy, destinationFormation)).ToArray();
    }

    private bool IsEligibleAdditionalEnemyPlacement(BattleEnemyPlacement enemy, int destinationFormation)
    {
        if (!IsOrdinaryEnemyPlacement(enemy))
        {
            return false;
        }
        HashSet<int> explicitEnemyModels = [];
        HashSet<BattleEncounterRosterRestrictionReason> restrictions = [];
        return EnemyAdmissions(enemy.ModelId, destinationFormation, explicitEnemyModels, restrictions).All(admission => admission == ExpandedAiAdmission.Supported) &&
            explicitEnemyModels.Count == 0 && restrictions.Count == 0 &&
            BattleResources.LoadEnemy(enemy.ModelId).Sequences?.CanCloneIndependently() == true;
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

    private List<(int Formation, BattleEnemyPlacement[] Enemies)> BuildAdditionalEnemyDonorPool(BattleEncounter encounter)
    {
        List<(int Formation, BattleEnemyPlacement[] Enemies)> pool = [];
        foreach (int formation in (ReadLocalEnemyFormations() ?? []).Distinct())
        {
            if (formation >= BattleResources.FormationCount)
            {
                continue;
            }
            BattleEncounter candidate = BattleResources.LoadEncounter(formation);
            BattleEnemyPlacement[] enemies = EligibleAdditionalEnemyPlacements(candidate, encounter.Id);
            if (enemies.Length != 0)
            {
                pool.Add((formation, enemies));
            }
        }
        if (pool.Count == 0)
        {
            BattleEnemyPlacement[] enemies = EligibleAdditionalEnemyPlacements(encounter, encounter.Id);
            if (enemies.Length != 0)
            {
                pool.Add((encounter.Id, enemies));
            }
        }
        return pool;
    }

    private ImmutableSortedDictionary<int, BattleEnemyRosterSource> SelectAdditionalEnemies(BattleEncounter encounter, int count, byte seed)
    {
        int available = Math.Min(count, ExpandedBattleFormation.MaximumEnemies - encounter.Enemies.Count);
        ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Builder sources = ImmutableSortedDictionary.CreateBuilder<int, BattleEnemyRosterSource>();
        if (available <= 0)
        {
            return sources.ToImmutable();
        }
        // Selection is replayable from the entry seed and does not spend battle or exploration RNG.
        NativeBattleRandomState random = NativeBattleRandomState.ForecastPhase(seed);
        List<(int Formation, BattleEnemyPlacement[] Enemies)> pool = BuildAdditionalEnemyDonorPool(encounter);
        while (pool.Count != 0 && sources.Count < available)
        {
            List<(int Formation, BattleEnemyPlacement[] Enemies)> round = [.. pool];
            while (round.Count != 0 && sources.Count < available)
            {
                int formationIndex = DrawIndex(round.Count);
                (int formation, BattleEnemyPlacement[] enemies) = round[formationIndex];
                round.RemoveAt(formationIndex);
                string hash = BattleResources.FormationSourceHash(formation);
                List<BattleEnemyPlacement> placements = [.. enemies];
                while (placements.Count != 0 && sources.Count < available)
                {
                    int enemyIndex = DrawIndex(placements.Count);
                    sources.Add(ExpandedBattleFormation.OriginalEnemyPrototypeCount + sources.Count, new(formation, placements[enemyIndex].Slot, hash));
                    placements.RemoveAt(enemyIndex);
                }
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

    private BattleEnemyPlacement[] ResolveExpandedEnemyPlacements(BattleEncounter encounter, ImmutableSortedDictionary<int, BattleEnemyRosterSource> addedEnemies, bool originalSlots = false, IReadOnlyList<BattleEnemyPlacement>? originalPlacements = null, bool diagnosticRoster = false)
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
        Dictionary<int, BattleEnemyPlacement[]> donors = addedEnemies.Count == 0
            ? []
            : diagnosticRoster
                ? addedEnemies.Values.Select(source => source.Formation).Distinct().ToDictionary(formation => formation, formation => EligibleAdditionalEnemyPlacements(BattleResources.LoadEncounter(formation), encounter.Id))
                : BuildAdditionalEnemyDonorPool(encounter).ToDictionary(donor => donor.Formation, donor => donor.Enemies);
        foreach ((int slot, BattleEnemyRosterSource source) in addedEnemies)
        {
            if (source is null)
            {
                throw new InvalidDataException("The added enemy roster has no selected placement source.");
            }
            source.Validate();
            if (!donors.TryGetValue(source.Formation, out BattleEnemyPlacement[]? eligible))
            {
                throw new InvalidDataException($"Added enemy source formation {source.Formation} is outside the suspended exploration encounter table's eligible pool and its destination fallback.");
            }
            if (source.Formation >= BattleResources.FormationCount || !BattleResources.FormationSourceHash(source.Formation).Equals(source.FormationHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The added enemy roster disagrees with its installed source formation.");
            }
            BattleEnemyPlacement enemy = eligible.SingleOrDefault(candidate => candidate.Slot == source.SceneSlot)
                ?? throw new InvalidDataException("The added enemy no longer has an eligible independent installed placement.");
            if (diagnosticRoster && placements.Any(candidate => candidate.Enabled && candidate.ModelId == enemy.ModelId))
            {
                throw new InvalidDataException("Diagnostic roster donors must have distinct models and exclude the required enemy's model.");
            }
            placements.Add(enemy with { Slot = slot });
        }
        return placements.ToArray();
    }
}
