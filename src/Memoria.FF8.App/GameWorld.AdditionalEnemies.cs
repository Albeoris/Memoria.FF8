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
            || encounter.Slots.Any(slot => !slot.Enabled && IsReservePlacement(slot));
    }

    private static bool IsReservePlacement(BattleEnemyPlacement slot)
    {
        return slot.IsEnemyModel && slot.NativeModelId != 255 && (slot.ModelId != 0 || slot.Hidden || slot.Untargetable || slot.InitiallySuspended);
    }

    private static bool IsOrdinaryEnemyPlacement(BattleEnemyPlacement slot)
    {
        return slot.IsEnemyModel && slot.NativeModelId != 255 && slot.Enabled && !slot.Hidden && !slot.Untargetable && !slot.InitiallySuspended;
    }

    private bool HasAuthoredEnemyAddresses(int model, int formation)
    {
        List<string> explicitModels = [];
        return EnemyAdmissions(model, formation, explicitModels).Contains(ExpandedAiAdmission.RequiresOriginalSlots) || explicitModels.Count != 0;
    }

    private ExpandedAiAdmission[] EnemyAdmissions(int model, int formation, ICollection<string>? explicitModels = null)
    {
        NativeBattleAiDefinition program = BattleResources.ReadEnemyAi(model, 0);
        return Enumerable.Range(0, 3).Select(band => NativeBattleExpandedAi.Evaluate(program with { LevelBand = band }, BattleResources.NativeActions, formation, explicitModelReferences: explicitModels)).ToArray();
    }

    private ImmutableArray<ushort> ReadLocalEnemyFormations()
    {
        if (session.State.Scene == SceneKind.Field && Field?.RandomEncounters is { Rate: > 0 } field)
        {
            return field.Formations;
        }
        if (session.State.Scene == SceneKind.WorldMap && worldScene is { } world && world.State.GroundType.HasValue && world.ReadRandomEncounterDefinition(world.State) is { } terrain)
        {
            return terrain.Formations;
        }
        return [];
    }

    private ImmutableSortedDictionary<int, BattleEnemyRosterSource> SelectAdditionalEnemies(BattleEncounter encounter, int count, byte seed)
    {
        int originalCount = encounter.Enemies.Count;
        int available = Math.Min(count, ExpandedBattleFormation.MaximumSideSize - originalCount);
        ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Builder sources = ImmutableSortedDictionary.CreateBuilder<int, BattleEnemyRosterSource>();
        if (available <= 0)
        {
            return sources.ToImmutable();
        }
        // Selection is replayable from the entry seed and does not spend battle or exploration RNG.
        NativeBattleRandomState random = NativeBattleRandomState.ForecastPhase(seed);
        List<(int Formation, BattleEnemyPlacement[] Enemies)> pool = [];
        foreach (int formation in ReadLocalEnemyFormations().Distinct())
        {
            if (formation >= BattleResources.FormationCount)
            {
                continue;
            }
            BattleEncounter candidate = BattleResources.LoadEncounter(formation);
            if (NeedsOriginalEnemySlots(candidate))
            {
                continue;
            }
            BattleEnemyPlacement[] enemies = candidate.Enemies.Where(enemy =>
            {
                List<string> explicitModels = [];
                return EnemyAdmissions(enemy.ModelId, encounter.Id, explicitModels).All(admission => admission == ExpandedAiAdmission.Supported) && explicitModels.Count == 0;
            }).ToArray();
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
        foreach ((int slot, BattleEnemyRosterSource source) in addedEnemies)
        {
            source.Validate();
            if (slot != placements.Count || slot >= ExpandedBattleFormation.MaximumSideSize || !BattleResources.FormationSourceHash(source.Formation).Equals(source.FormationHash, StringComparison.OrdinalIgnoreCase))
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
