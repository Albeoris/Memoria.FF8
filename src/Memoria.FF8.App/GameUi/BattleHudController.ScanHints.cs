using System.Globalization;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private readonly record struct HintSelection(BattleCombatant Actor, BattleAbility Ability, BattleActionDefinition? Action, bool SmartAttack);
    private sealed record CachedResistanceRow(BattleResistanceHintProfile Profile, int?[] Percentages, BattleResistanceBadge[] Badges, CultureInfo Culture, long CatalogGeneration);

    private readonly Dictionary<string, CachedResistanceRow> resistanceRows = [];
    private HintSelection? drawHintSelection;
    private LocalizationBattlePresentation? hintLocalization;

    public BattleScanHintMode ScanHintMode { get; set; } = BattleScanHintMode.Targets;

    internal BattleResistanceTone MagicHintTone(BattleCombatant caster, BattleActionDefinition action)
    {
        if (ScanHintMode != BattleScanHintMode.Full)
        {
            return BattleResistanceTone.Normal;
        }
        BattleResistanceHintProfile profile = session.GetResistanceHintProfile(caster, BattleAbility.Magic, action);
        if (!profile.CanRate)
        {
            return BattleResistanceTone.Normal;
        }
        bool hasEnemy = false;
        bool allIneffective = true;
        for (int index = 0; index < session.Combatants.Count; index++)
        {
            BattleCombatant target = session.Combatants[index];
            if (target.IsPlayer || target.Hidden || !BattleSession.IsValidSelectedActionTarget(caster, action, target))
            {
                continue;
            }
            hasEnemy = true;
            BattleResistanceHintRating rating = session.RateResistanceHints(target, profile);
            if (rating == BattleResistanceHintRating.Favorable)
            {
                return BattleResistanceTone.Favorable;
            }
            allIneffective &= rating == BattleResistanceHintRating.Ineffective;
        }
        return hasEnemy && allIneffective ? BattleResistanceTone.Warning : BattleResistanceTone.Normal;
    }

    internal IReadOnlyList<BattleResistanceBadge> ResistanceBadges(BattleCombatant target)
    {
        if (ScanHintMode == BattleScanHintMode.Off || target.IsPlayer || target.Hidden || !target.IsAlive || !target.IsActive || target.Untargetable)
        {
            return [];
        }
        HintSelection? selection = drawingPresentation ? drawHintSelection ??= ReadHintSelection() : ReadHintSelection();
        if (selection is not { } selected)
        {
            return [];
        }
        bool known = session.IsPartyScanKnown(target);
        if (!SelectingTarget && (!known || selected.Action is null || !BattleSession.IsValidSelectedActionTarget(selected.Actor, selected.Action, target)))
        {
            return [];
        }
        if (selected.SmartAttack && session.CanSmartCast(selected.Actor, target))
        {
            // Smart Attack picks a different command for each recipient. Its caption owns the focused preview.
            if (target.Id != selectedTargetId)
            {
                return [];
            }
            if (TargetActionPreview is { Ability: BattleAbility.Magic or BattleAbility.Draw } preview)
            {
                selected = selected with { Ability = BattleAbility.Magic, Action = SmartSpell(preview.ActionId) };
            }
        }
        BattleResistanceHintProfile profile = session.GetResistanceHintProfile(selected.Actor, selected.Ability, selected.Action);
        if (profile.IsEmpty)
        {
            return [];
        }
        int elementCount = profile.ElementIndex.HasValue ? 1 : 0;
        Span<int?> percentages = stackalloc int?[elementCount + BitOperations.PopCount(profile.StatusMask)];
        int count = 0;
        if (profile.ElementIndex is int element)
        {
            percentages[count++] = known ? session.GetKnownElementResistance(target, element) : null;
        }
        for (ulong mask = profile.StatusMask; mask != 0; mask &= mask - 1)
        {
            var status = (NativeStatusId)BitOperations.TrailingZeroCount(mask);
            percentages[count++] = known ? session.GetKnownStatusResistance(target, status) : null;
        }
        CultureInfo culture = CultureInfo.CurrentCulture;
        long generation = hintLocalization?.CatalogGeneration ?? 0;
        if (resistanceRows.TryGetValue(target.Id, out CachedResistanceRow? cached) && cached.Profile == profile && cached.Culture == culture && cached.CatalogGeneration == generation && percentages.SequenceEqual(cached.Percentages, EqualityComparer<int?>.Default))
        {
            return cached.Badges;
        }
        BattleResistanceBadge[] badges = new BattleResistanceBadge[count];
        int index = 0;
        if (profile.ElementIndex is int elementIndex)
        {
            int? percent = percentages[index];
            badges[index] = Badge(TargetScanScreen.Element(elementIndex, percent.GetValueOrDefault(), localizeSystemText), percent.HasValue, false);
            index++;
        }
        for (ulong mask = profile.StatusMask; mask != 0; mask &= mask - 1)
        {
            var status = (NativeStatusId)BitOperations.TrailingZeroCount(mask);
            int? percent = percentages[index];
            badges[index] = Badge(TargetScanScreen.Status(status, percent.GetValueOrDefault(), localizeSystemText), percent.HasValue, true);
            index++;
        }
        resistanceRows[target.Id] = new(profile, percentages.ToArray(), badges, culture, generation);
        return badges;
    }

    private HintSelection? ReadHintSelection()
    {
        if ((!SelectingTarget && (ScanHintMode != BattleScanHintMode.Full || !ShowingMagicList)) || SelectedActor is not { } actor)
        {
            return null;
        }
        if (HasGuardianFormCommands)
        {
            BattleActionDefinition formAction = SelectedGuardianFormCommand.Action;
            return new(actor, formAction.Ability, formAction, false);
        }
        return SelectedCommand switch
        {
            BattleAbility.Attack => new(actor, BattleAbility.Attack, null, !ForcePhysical),
            BattleAbility.Magic or BattleAbility.GuardianForce when SelectedAction is { } action => new(actor, action.Ability, action, false),
            BattleAbility.Draw when CastSelectedDraw && SelectedAction is { } spell && session.KnownMagic?.Contains(spell.Id) != false => new(actor, BattleAbility.Magic, spell, false),
            _ => null
        };
    }

    private static BattleResistanceBadge Badge(TargetScanResistance row, bool known, bool status)
    {
        return known
            ? new(row.Icon, row.Name, TargetScanScreen.Percent(row.Percent), BattleResistanceColors.Tone(row.Percent, status))
            : new(row.Icon, row.Name, "??", BattleResistanceTone.Normal);
    }
}
