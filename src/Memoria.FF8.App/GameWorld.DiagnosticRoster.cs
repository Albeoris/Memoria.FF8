using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticBattleRoster([property: JsonRequired] ImmutableArray<BattleEnemyRosterSource> Donors, [property: JsonRequired] int OriginalSceneSlot)
{
    public int? DonorCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MountedReserveSceneSlot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthoredFormationHash { get; init; }

    internal void Validate()
    {
        int count = DonorCount ?? (Donors.IsDefault ? 0 : Donors.Length);
        bool authoredOnly = DonorCount == 0 && AuthoredFormationHash is not null && !Donors.IsDefault && Donors.IsEmpty;
        if (Donors.IsDefault || !authoredOnly && count < 1 || Donors.Length > (DonorCount.HasValue ? 256 : ExpandedBattleFormation.MaximumAddedEnemies) ||
            count > ExpandedBattleFormation.MaximumAddedEnemies || count > Donors.Length || Donors.Any(source => source is null) ||
            Donors.Select(source => (source.Formation, source.SceneSlot)).Distinct().Count() != Donors.Length ||
            OriginalSceneSlot is < 0 or >= ExpandedBattleFormation.OriginalEnemyPrototypeCount ||
            MountedReserveSceneSlot is < 0 or >= ExpandedBattleFormation.OriginalEnemyPrototypeCount || MountedReserveSceneSlot == OriginalSceneSlot)
        {
            throw new InvalidDataException("Diagnostic roster requires distinct installed donor placements and one declared active original enemy.");
        }
        foreach (BattleEnemyRosterSource source in Donors)
        {
            source.Validate();
        }
        if (AuthoredFormationHash is { } hash && (hash.Length != 64 || !hash.All(Uri.IsHexDigit) || MountedReserveSceneSlot is not null))
        {
            throw new InvalidDataException("Diagnostic authored roster requires an installed formation hash and no separate mounted reserve.");
        }
    }
}

internal sealed record DiagnosticMountedReserveReceipt(long DispatchSerial, long DispatchCallback, DiagnosticBattleRosterMember[] MembersAtDispatch, BattleDiagnosticTarget ReserveAtDispatch)
{
    public long? ActivationCallback { get; init; }
    public DiagnosticBattleRosterMember[]? MembersAfterActivation { get; init; }
}

internal sealed partial class GameWorld
{
    private ImmutableSortedDictionary<int, BattleEnemyRosterSource> SelectDiagnosticRosterDonors(BattleEncounter encounter, DiagnosticBattleRoster roster, HashSet<int> reserveSlots)
    {
        if (roster.DonorCount == 0)
        {
            return ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Empty;
        }
        int count = roster.DonorCount ?? roster.Donors.Length;
        var selected = ImmutableSortedDictionary.CreateBuilder<int, BattleEnemyRosterSource>();
        HashSet<int> models = SelectDiagnosticRosterOriginals(encounter, roster, reserveSlots).Where(enemy => enemy.Enabled).Select(enemy => enemy.ModelId).ToHashSet();
        foreach (BattleEnemyRosterSource source in roster.Donors)
        {
            if (source.Formation >= BattleResources.FormationCount || !BattleResources.FormationSourceHash(source.Formation).Equals(source.FormationHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The diagnostic candidate differs from its installed source formation.");
            }
            BattleEncounter formation = BattleResources.LoadEncounter(source.Formation);
            BattleDonorGroup? group = ReadEligibleDonorGroups(formation, encounter.Id).SingleOrDefault(candidate => candidate.Root.Slot == source.SceneSlot);
            int[] groupModels = group?.Members.Where(actor => actor.Enabled).Select(actor => actor.ModelId).ToArray() ?? [];
            if (group is null || group.Members.Length > count - selected.Count || groupModels.Distinct().Count() != groupModels.Length || groupModels.Any(models.Contains))
            {
                if (roster.DonorCount is null)
                {
                    throw new NotSupportedException($"Diagnostic source formation {source.Formation}, slot {source.SceneSlot} has no compatible complete donor group within its roster.");
                }
                continue;
            }

            AddDonorGroup(selected, formation, group);
            models.UnionWith(groupModels);
            if (selected.Count == count)
            {
                return selected.ToImmutable();
            }
        }
        throw new NotSupportedException($"Diagnostic roster has fewer than {count} eligible installed donor placements with complete source groups.");
    }

    private static NativeCharacterVitals PrepareDiagnosticRosterVitals(NativeCharacterVitals initial)
    {
        NativeCharacterVitals extended = initial.WithExtendedRoster(true).Rest();
        return extended with { Records = extended.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1) }).ToArray() };
    }

    private BattleEnemyPlacement[] SelectDiagnosticRosterOriginals(BattleEncounter encounter, DiagnosticBattleRoster roster, HashSet<int> reserveSlots)
    {
        BattleEnemyPlacement caster = encounter.Enemies.SingleOrDefault(enemy => enemy.Slot == roster.OriginalSceneSlot && IsOrdinaryEnemyPlacement(enemy))
            ?? throw new InvalidDataException("The declared original diagnostic enemy must be an enabled visible encounter participant.");
        BattleEnemyPlacement[] prototypes = encounter.Slots.Where(enemy => reserveSlots.Contains(enemy.Slot) && IsEnemyPrototype(enemy) && !enemy.Enabled).ToArray();
        if (diagnosticBattle?.Request is { Prelude.RequireNativeAllocation: true, Target: { } target } && !prototypes.Any(enemy => enemy.Slot == target.SceneSlot && enemy.ModelId == target.Model))
        {
            throw new InvalidDataException("The diagnostic allocation target must have its original referenced inactive enemy prototype.");
        }
        if (roster.AuthoredFormationHash is { } hash)
        {
            if (!BattleResources.FormationSourceHash(encounter.Id).Equals(hash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The diagnostic authored roster differs from its installed formation.");
            }
            BattleEnemyPlacement[] authored = AuthoredEnemyPlacements(encounter).Where(enemy => enemy.Enabled).ToArray();
            if (diagnosticBattle?.Request.Driver is { EnemyTrigger: not null, TerminalHold: not null } && authored.Length != 1)
            {
                throw new InvalidDataException("A death trigger victory hold requires the sole active original enemy.");
            }
            return authored.Concat(prototypes).OrderBy(enemy => enemy.Slot).ToArray();
        }
        if (roster.MountedReserveSceneSlot is not int reserveSlot)
        {
            return [caster, .. prototypes];
        }
        BattleEnemyPlacement mounted = encounter.Slots.SingleOrDefault(enemy => enemy.Slot == reserveSlot &&
            IsEnemyPrototype(enemy) && enemy.Enabled && enemy.Hidden && enemy.InitiallySuspended && enemy.Untargetable)
            ?? throw new InvalidDataException("The declared mounted reserve must be an enabled, hidden, suspended and untargetable original participant.");
        return [caster, mounted, .. prototypes];
    }

    private static int? DiagnosticOriginalEnemyOrdinal(BattleEncounter encounter, DiagnosticBattleRoster? roster, BattleEnemyPlacement enemy, bool original)
    {
        if (!original || !enemy.Enabled || roster is not ({ MountedReserveSceneSlot: not null } or { AuthoredFormationHash: not null }))
        {
            return null;
        }
        int ordinal = Array.FindIndex(encounter.Slots.Where(slot => slot.Enabled && IsEnemyPrototype(slot)).ToArray(), slot => slot.Slot == enemy.Slot);
        if (ordinal < 0)
        {
            throw new InvalidDataException("The retained participant has no installed initial physical alias.");
        }
        return ordinal;
    }

    internal static DiagnosticBattleRosterMember[] ObserveDiagnosticRosterMembers(BattleSession active, bool includeMountedReserve = true)
    {
        return active.Combatants.Where(actor => actor.IsPlayer || actor.IsActive && (includeMountedReserve || !actor.Hidden || !actor.Suspended || !actor.Untargetable))
            .Select(actor => new DiagnosticBattleRosterMember(actor.Id, actor.ModelId, actor.IsPlayer, actor.SceneSlot)).ToArray();
    }
}
