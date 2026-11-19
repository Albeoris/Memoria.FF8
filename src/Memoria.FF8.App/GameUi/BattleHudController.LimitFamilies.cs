using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    public bool SelectingNativeLimitFamilies { get; private set; }
    private bool nativeLimitFamilyChosen;

    private IReadOnlyList<BattleActionDefinition> NativeLimitFamilyActions => SelectedActor is { } actor ? session.GetNativeLimitFamilies(actor.Id).Select(entry =>
    {
        string? reason = entry.UnavailableReason ?? (session.GetNativeLimitLevel(actor.Id) == 0 ? SystemText("battle.ui.limitfamilies.notready", "Limit is not ready.") : null);
        return new BattleActionDefinition(BattleAbility.LimitBreak, (int)entry.Family, reason is null ? entry.Name : entry.Name + " · " + reason, BattleActionEffect.Native, 0, false) { Available = entry.Available && reason is null };
    }).ToArray() : [];

    private bool PrepareNativeLimitFamilySelection(BattleCombatant actor)
    {
        if (SelectedCommand != BattleAbility.LimitBreak || actor.NativeLimitFamilies is null)
        {
            return false;
        }
        if (SelectingNativeLimitFamilies)
        {
            BattleActionDefinition? selected = SelectedAction;
            if (selected is null || !selected.Available)
            {
                ShowCommandError(selected?.Name ?? "No assigned Limit families.");
                return true;
            }
            session.SelectNativeLimitFamily(actor.Id, (BattleLimitFamily)selected.Id);
            SelectingNativeLimitFamilies = false;
            SelectingAction = false;
            nativeLimitFamilyChosen = true;
            actionIndex = 0;
            return false;
        }
        if (SelectingAction || SelectingTarget || nativeLimitFamilyChosen)
        {
            return false;
        }
        IReadOnlyList<BattleNativeLimitFamilyChoice> families = session.GetNativeLimitFamilies(actor.Id);
        if (families.Count == 1 && families[0].Available && session.GetNativeLimitLevel(actor.Id) > 0)
        {
            session.SelectNativeLimitFamily(actor.Id, families[0].Family);
            nativeLimitFamilyChosen = true;
            return false;
        }
        SelectingNativeLimitFamilies = true;
        SelectingAction = true;
        actionIndex = 0;
        SetSystemMessage(() => "Choose a Limit family.");
        SoundRequested?.Invoke(MenuSound.Confirm);
        return true;
    }

    private bool CancelNativeLimitFamilySelection()
    {
        if (SelectedCommand != BattleAbility.LimitBreak || SelectedActor?.NativeLimitFamilies is not { Count: > 0 } families)
        {
            return false;
        }
        if (SelectingNativeLimitFamilies)
        {
            SelectingNativeLimitFamilies = false;
            SelectingAction = false;
            SelectingTarget = false;
            nativeLimitFamilyChosen = false;
            SetSystemMessage(() => "Choose a command.");
        }
        else if (families.Count > 1 && SelectingAction && (!SelectingTarget || NativeLimitDirectTarget))
        {
            SelectingNativeLimitFamilies = true;
            SelectingTarget = false;
            nativeLimitFamilyChosen = false;
            session.CloseNativeActionMenu();
            actionIndex = 0;
            SetSystemMessage(() => "Choose a Limit family.");
        }
        else
        {
            return false;
        }
        SoundRequested?.Invoke(MenuSound.Cancel);
        return true;
    }
}
