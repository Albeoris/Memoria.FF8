using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticBattleRoster(ImmutableArray<BattleEnemyRosterSource> Donors, int? OriginalSceneSlot)
{
    internal void Validate()
    {
        if (Donors.IsDefaultOrEmpty || Donors.Length > ExpandedBattleFormation.MaximumAddedEnemies || Donors.Any(source => source is null) ||
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
    private static NativeCharacterVitals PrepareDiagnosticRosterVitals(NativeCharacterVitals initial)
    {
        NativeCharacterVitals extended = initial.WithExtendedRoster(true).Rest();
        return extended with { Records = extended.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1) }).ToArray() };
    }

    private static BattleEnemyPlacement[] SelectDiagnosticRosterOriginals(BattleEncounter encounter, DiagnosticBattleRoster roster)
    {
        if (roster.OriginalSceneSlot is not int sceneSlot)
        {
            return [];
        }
        BattleEnemyPlacement caster = encounter.Enemies.SingleOrDefault(enemy => enemy.Slot == sceneSlot && IsOrdinaryEnemyPlacement(enemy))
            ?? throw new InvalidDataException("The declared original diagnostic enemy must be an enabled visible encounter participant.");
        return [caster];
    }
}
