using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticBattleRoster([property: JsonRequired] ImmutableArray<BattleEnemyRosterSource> Donors, [property: JsonRequired] int OriginalSceneSlot)
{
    public int? DonorCount { get; init; }

    internal void Validate()
    {
        int count = DonorCount ?? (Donors.IsDefault ? 0 : Donors.Length);
        if (Donors.IsDefaultOrEmpty || Donors.Length > (DonorCount.HasValue ? 256 : ExpandedBattleFormation.MaximumAddedEnemies) ||
            count < 1 || count > ExpandedBattleFormation.MaximumAddedEnemies || count > Donors.Length || Donors.Any(source => source is null) ||
            Donors.Select(source => (source.Formation, source.SceneSlot)).Distinct().Count() != Donors.Length ||
            OriginalSceneSlot is < 0 or >= ExpandedBattleFormation.OriginalEnemyPrototypeCount)
        {
            throw new InvalidDataException("Diagnostic roster requires distinct installed donor placements and at most one original enemy.");
        }
        foreach (BattleEnemyRosterSource source in Donors)
        {
            source.Validate();
        }
    }
}

internal sealed partial class GameWorld
{
    private ImmutableSortedDictionary<int, BattleEnemyRosterSource> SelectDiagnosticRosterDonors(BattleEncounter encounter, DiagnosticBattleRoster roster)
    {
        IEnumerable<BattleEnemyRosterSource> selected = roster.Donors;
        if (roster.DonorCount is int count)
        {
            HashSet<int> models = SelectDiagnosticRosterOriginals(encounter, roster).Select(enemy => enemy.ModelId).ToHashSet();
            selected = roster.Donors.Where(IsEligible).Take(count).ToArray();
            if (selected.Count() != count)
            {
                throw new NotSupportedException($"Diagnostic roster has fewer than {count} eligible independent installed donor models.");
            }

            bool IsEligible(BattleEnemyRosterSource source)
            {
                if (source.Formation >= BattleResources.FormationCount || !BattleResources.FormationSourceHash(source.Formation).Equals(source.FormationHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The diagnostic candidate differs from its installed source formation.");
                }
                BattleEnemyPlacement? candidate = BattleResources.LoadEncounter(source.Formation).Enemies.FirstOrDefault(enemy => enemy.Slot == source.SceneSlot);
                return candidate is not null && !models.Contains(candidate.ModelId) && IsEligibleAdditionalEnemyPlacement(candidate, encounter.Id) && models.Add(candidate.ModelId);
            }
        }
        return selected.Select((source, ordinal) => KeyValuePair.Create(ExpandedBattleFormation.OriginalEnemyPrototypeCount + ordinal, source)).ToImmutableSortedDictionary();
    }

    private static NativeCharacterVitals PrepareDiagnosticRosterVitals(NativeCharacterVitals initial)
    {
        NativeCharacterVitals extended = initial.WithExtendedRoster(true).Rest();
        return extended with { Records = extended.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1) }).ToArray() };
    }

    private static BattleEnemyPlacement[] SelectDiagnosticRosterOriginals(BattleEncounter encounter, DiagnosticBattleRoster roster)
    {
        BattleEnemyPlacement caster = encounter.Enemies.SingleOrDefault(enemy => enemy.Slot == roster.OriginalSceneSlot && IsOrdinaryEnemyPlacement(enemy))
            ?? throw new InvalidDataException("The declared original diagnostic enemy must be an enabled visible encounter participant.");
        return [caster];
    }
}
