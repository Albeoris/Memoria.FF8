using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private enum MagicSection
    {
        GroupAttack,
        SingleAttack,
        Buffs,
        Recovery,
        Debuffs,
        Other
    }

    private MagicSection magicSection;
    private IReadOnlyList<BattleActionDefinition>? presentationMagicStockChoices;

    public bool ShowingMagicList => SelectedCommand == BattleAbility.Magic && SelectingAction && !SelectingTarget && !HasGuardianFormCommands;
    private MagicSection CurrentMagicSection => MagicSections.Contains(magicSection) ? magicSection : MagicSections.FirstOrDefault();
    private MagicSection[] MagicSections => MagicStockChoices().Select(ClassifyMagic).Distinct().Order().ToArray();
    public bool MagicPreviousSectionAvailable => MagicSections.ToList().IndexOf(CurrentMagicSection) > 0;
    public bool MagicNextSectionAvailable => MagicSections.ToList().IndexOf(CurrentMagicSection) is int index && index >= 0 && index < MagicSections.Length - 1;
    public string MagicSectionCaption => CurrentMagicSection switch
    {
        MagicSection.GroupAttack => SystemText("battle.ui.magic.section.group.attack", "Multi-target attack"),
        MagicSection.SingleAttack => SystemText("battle.ui.magic.section.single.attack", "Single-target attack"),
        MagicSection.Buffs => SystemText("battle.ui.magic.section.buffs", "Buffs"),
        MagicSection.Recovery => SystemText("battle.ui.magic.section.recovery", "Recovery"),
        MagicSection.Debuffs => SystemText("battle.ui.magic.section.debuffs", "Debuffs"),
        _ => SystemText("battle.ui.magic.section.other", "Other")
    };

    private static MagicSection ClassifyMagic(BattleActionDefinition action)
    {
        if (action.Effect == BattleActionEffect.RemoveStatus || action.NativeAction is { Power: 0, AttackType: 3 or 4 or 21 or 32 })
        {
            return action.Allies ? MagicSection.Recovery : MagicSection.Debuffs;
        }
        if (action.Effect is BattleActionEffect.Heal or BattleActionEffect.FullHeal or BattleActionEffect.Revive || action.NativeAction?.AttackType is 3 or 4 or 5 or 6 or 21 or 25 or 32)
        {
            return MagicSection.Recovery;
        }
        if (action.Effect == BattleActionEffect.Damage || action.NativeAction is { Power: > 0, AttackType: 1 or 7 or 8 or 9 or 10 or 11 or 15 or 18 or 20 or 22 or 26 or 27 or 28 or 29 or 33 or 34 or 35 or 36 })
        {
            bool randomGroup = action.NativeAction is { HitCount: > 1, TargetFlags: byte flags } && (flags & 2) != 0;
            return action.AllTargets || randomGroup ? MagicSection.GroupAttack : MagicSection.SingleAttack;
        }
        if (action.Effect == BattleActionEffect.Status)
        {
            return action.Allies ? MagicSection.Buffs : MagicSection.Debuffs;
        }
        return MagicSection.Other;
    }

    private IReadOnlyList<BattleActionDefinition> MagicStockChoices()
    {
        return drawingPresentation ? presentationMagicStockChoices ??= ReadMagicStockChoices() : ReadMagicStockChoices();
    }

    private IReadOnlyList<BattleActionDefinition> ReadMagicStockChoices()
    {
        if (SelectedActor is not { } actor)
        {
            return [];
        }
        return session.ActionDefinitions.Where(action => action.Ability == BattleAbility.Magic && actor.MagicStocks.Any(stock => stock.Id == action.Id && stock.Count > 0) && session.QuoteMagic(actor.Id, action.Id).Capacity > 0).OrderBy(action => action.Id).ToArray();
    }

    private IReadOnlyList<BattleActionDefinition> MagicActionChoices()
    {
        MagicSection section = CurrentMagicSection;
        return MagicStockChoices().Where(action => ClassifyMagic(action) == section).ToArray();
    }

    public void ChangeMagicSection(int direction)
    {
        if (!ShowingMagicList || direction == 0)
        {
            return;
        }
        MagicSection[] sections = MagicSections;
        int index = Array.IndexOf(sections, CurrentMagicSection);
        int next = index + Math.Sign(direction);
        if (next < 0 || next >= sections.Length)
        {
            return;
        }
        magicSection = sections[next];
        actionIndex = 0;
        SoundRequested?.Invoke(MenuSound.Move);
    }

    internal void HoverMagicChoice(int index)
    {
        if (ShowingMagicList && index >= 0 && index < ActionChoices.Count && actionIndex != index)
        {
            actionIndex = index;
            SoundRequested?.Invoke(MenuSound.Move);
        }
    }

    private bool NavigateMagicList(MenuInput input)
    {
        if (!ShowingMagicList || input is not (MenuInput.Up or MenuInput.Down or MenuInput.Left or MenuInput.Right or MenuInput.PageLeft or MenuInput.PageRight or MenuInput.PageUp or MenuInput.PageDown or MenuInput.Alternate))
        {
            return false;
        }
        if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            ChangeMagicSection(input == MenuInput.PageLeft ? -1 : 1);
            return true;
        }
        if (input is MenuInput.PageUp or MenuInput.PageDown)
        {
            ChangeMagicCastCount(input == MenuInput.PageUp ? -1 : 1);
            return true;
        }
        if (input is MenuInput.Up or MenuInput.Down or MenuInput.Left or MenuInput.Right)
        {
            BattleDrawLayout layout = BattleDrawLayout.Calculate(ActionChoices.Count);
            (int column, int row) = layout.GetCell(ChoiceIndex);
            int next = actionIndex;
            if (input is MenuInput.Up or MenuInput.Down && layout.ColumnRows(column) > 0)
            {
                next = layout.GetIndex(column, (row + (input == MenuInput.Up ? -1 : 1) + layout.ColumnRows(column)) % layout.ColumnRows(column));
            }
            else if (input == MenuInput.Left && column == 0 || input == MenuInput.Right && column == layout.Columns - 1)
            {
                ChangeMagicSection(input == MenuInput.Left ? -1 : 1);
                return true;
            }
            else if (input is MenuInput.Left or MenuInput.Right)
            {
                column += input == MenuInput.Left ? -1 : 1;
                next = layout.GetIndex(column, Math.Min(row, layout.ColumnRows(column) - 1));
            }
            if (next >= 0 && next != actionIndex)
            {
                actionIndex = next;
                SoundRequested?.Invoke(MenuSound.Move);
            }
            return true;
        }
        BattleMagicRecoveryChoice? recovery = session.PreviewMagicRecovery(SelectedActor!.Id);
        if (recovery is null)
        {
            SoundRequested?.Invoke(MenuSound.Error);
            return true;
        }
        bool confirm = SelectedAction?.Id == recovery.ActionId;
        BattleActionDefinition action = MagicStockChoices().Single(action => action.Id == recovery.ActionId);
        magicSection = ClassifyMagic(action);
        actionIndex = ActionChoices.ToList().FindIndex(candidate => candidate.Id == recovery.ActionId);
        magicCastCount = 1;
        magicCastCountSelected = true;
        selectedMagicTargets.Clear();
        if (confirm)
        {
            Attack();
            if (SelectingTarget && SelectableTargets.Any(target => target.Id == recovery.TargetId))
            {
                selectedTargetId = recovery.TargetId;
            }
        }
        else
        {
            SoundRequested?.Invoke(MenuSound.Move);
        }
        return true;
    }
}
