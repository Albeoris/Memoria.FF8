using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private static IEnumerable<BattleEnemyPlacement> AuthoredEnemyPlacements(BattleEncounter encounter)
    {
        return encounter.Slots.Where(slot => slot.NativeModelId != 255 && (slot.Enabled || slot.NativeModelId != 0));
    }

    private bool NeedsOriginalEnemySlots(BattleEncounter encounter)
    {
        return encounter.Enemies.Any(slot => !IsOrdinaryEnemyPlacement(slot) || HasAuthoredEnemyAddresses(slot.ModelId, encounter.Id))
            || ReferencedReserveSlots(encounter).Count != 0;
    }

    private static bool IsEnemyPrototype(BattleEnemyPlacement slot)
    {
        return slot.IsEnemyModel && slot.NativeModelId != 255;
    }

    private static bool IsOrdinaryEnemyPlacement(BattleEnemyPlacement slot)
    {
        return IsEnemyPrototype(slot) && slot.Enabled && !slot.Hidden && !slot.Untargetable && !slot.InitiallySuspended;
    }

    private bool HasAuthoredEnemyAddresses(int model, int formation)
    {
        HashSet<int> explicitModels = [];
        return EnemyAdmissions(model, formation, explicitModels).Contains(ExpandedAiAdmission.RequiresOriginalSlots) || explicitModels.Count != 0;
    }

    private ExpandedAiAdmission[] EnemyAdmissions(int model, int formation, ISet<int> explicitEnemyModels)
    {
        NativeBattleAiDefinition program = BattleResources.ReadEnemyAi(model, 0);
        return Enumerable.Range(0, 3).Select(band => NativeBattleExpandedAi.Evaluate(program with { LevelBand = band }, BattleResources.NativeActions, formation, explicitEnemyModelReferences: explicitEnemyModels)).ToArray();
    }

    private HashSet<int> ReferencedReserveSlots(BattleEncounter encounter)
    {
        HashSet<int> slots = [];
        HashSet<int> visitedModels = [];
        Queue<BattleEnemyPlacement> pending = new(encounter.Enemies.Where(slot => slot.Enabled && IsEnemyPrototype(slot)));
        while (pending.TryDequeue(out BattleEnemyPlacement? placement))
        {
            if (!visitedModels.Add(placement.ModelId))
            {
                continue;
            }
            NativeBattleAiDefinition program = BattleResources.ReadEnemyAi(placement.ModelId, 0);
            HashSet<int> referenced = [];
            for (int band = 0; band < 3; band++)
            {
                NativeBattleExpandedAi.Evaluate(program with { LevelBand = band }, BattleResources.NativeActions, encounter.Id, originalEnemySlots: true, reserveSceneSlots: referenced);
            }
            foreach (int slot in referenced.Where(slots.Add))
            {
                if ((uint)slot >= ExpandedBattleFormation.OriginalEnemyPrototypeCount)
                {
                    throw new InvalidDataException($"Formation {encounter.Id} model {placement.NativeModelId} references reserve slot {slot} outside the original formation slots.");
                }
                BattleEnemyPlacement prototype = encounter.Slots.SingleOrDefault(candidate => candidate.Slot == slot)
                    ?? throw new InvalidDataException($"Formation {encounter.Id} model {placement.NativeModelId} references missing reserve slot {slot}.");
                if (!IsEnemyPrototype(prototype))
                {
                    throw new InvalidDataException($"Formation {encounter.Id} model {placement.NativeModelId} references reserve slot {slot} with nonenemy model {prototype.NativeModelId}.");
                }
                pending.Enqueue(prototype);
            }
        }
        return slots;
    }

    private BattleEnemyPlacement[] OriginalExpandedEnemyPlacements(BattleEncounter encounter, BattleDefinition? restoredDefinition)
    {
        HashSet<int> referenced = ReferencedReserveSlots(encounter);
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
        return candidate.Enemies.Where(enemy =>
        {
            if (!IsOrdinaryEnemyPlacement(enemy))
            {
                return false;
            }
            HashSet<int> explicitEnemyModels = [];
            return EnemyAdmissions(enemy.ModelId, destinationFormation, explicitEnemyModels).All(admission => admission == ExpandedAiAdmission.Supported) && explicitEnemyModels.Count == 0;
        }).ToArray();
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

    private ImmutableSortedDictionary<int, BattleEnemyRosterSource> SelectAdditionalEnemies(BattleEncounter encounter, int count, byte seed)
    {
        int originalCount = encounter.Enemies.Count;
        int available = Math.Min(count, ExpandedBattleFormation.MaximumEnemies - originalCount);
        ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Builder sources = ImmutableSortedDictionary.CreateBuilder<int, BattleEnemyRosterSource>();
        if (available <= 0)
        {
            return sources.ToImmutable();
        }
        // Selection is replayable from the entry seed and does not spend battle or exploration RNG.
        NativeBattleRandomState random = NativeBattleRandomState.ForecastPhase(seed);
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
                    sources.Add(originalCount + sources.Count, new(formation, placements[enemyIndex].Slot, hash));
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

    private BattleEnemyPlacement[] ResolveExpandedEnemyPlacements(BattleEncounter encounter, ImmutableSortedDictionary<int, BattleEnemyRosterSource> addedEnemies)
    {
        List<BattleEnemyPlacement> placements = encounter.Enemies.Select((enemy, index) => enemy with { Slot = index }).ToList();
        ImmutableArray<ushort>? localFormations = addedEnemies.Count == 0 ? null : ReadLocalEnemyFormations();
        foreach ((int slot, BattleEnemyRosterSource source) in addedEnemies)
        {
            source.Validate();
            if (localFormations is { } formations && (source.Formation > ushort.MaxValue || !formations.Contains((ushort)source.Formation)))
            {
                throw new InvalidDataException($"Added enemy source formation {source.Formation} is outside the suspended exploration encounter table.");
            }
            if (slot != placements.Count || slot >= ExpandedBattleFormation.MaximumEnemies || !BattleResources.FormationSourceHash(source.Formation).Equals(source.FormationHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The added enemy roster disagrees with its installed source formation.");
            }
            BattleEnemyPlacement enemy = BattleResources.LoadEncounter(source.Formation).Enemies.SingleOrDefault(candidate => candidate.Slot == source.SceneSlot && IsOrdinaryEnemyPlacement(candidate))
                ?? throw new InvalidDataException("The added enemy no longer has its selected installed placement.");
            placements.Add(enemy with { Slot = slot });
        }
        return placements.ToArray();
    }
}
