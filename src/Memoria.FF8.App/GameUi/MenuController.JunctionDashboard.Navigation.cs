using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed record JunctionDashboardNavigationState(string Editor, string Origin, string Candidate, bool Review, string Priority, string Focus, int CandidatePage, int ImpactPage, string Query, string Sort, JunctionEffectDirection ElementalDirection, JunctionEffectDirection StatusDirection, MenuAction? ReviewedAction = null, bool TransferModal = false);

public sealed partial class MenuController
{
    private IReadOnlyList<MenuEntry> JunctionDashboardEntries() => FocusableDescendants(BuildView().Root).OfType<UiButton>().Select(button => new MenuEntry(button.Id ?? "", JunctionControlLabel(button), J("select-preview", "Select · Preview"), Action: new MenuAction("junction-ui", button.Id ?? ""), Enabled: button.Enabled)).ToArray();

    internal JunctionDashboardNavigationState CaptureJunctionDashboard() => new(junctionEditor, junctionOrigin, junctionCandidate, junctionReview, junctionPriority, junctionFocus, junctionCandidatePage, junctionImpactPage, "", junctionSort, junctionDashboard.ElementalDirection, junctionDashboard.StatusDirection, junctionReviewedAction, junctionTransferModal);

    internal void RestoreJunctionDashboard(JunctionDashboardNavigationState saved)
    {
        if (!IsJunctionDashboard)
        {
            return;
        }
        junctionEditor = saved.Editor;
        junctionOrigin = saved.Origin;
        junctionCandidate = saved.Candidate;
        junctionPriority = saved.Priority;
        junctionSort = saved.Sort;
        junctionCandidatePage = saved.CandidatePage;
        junctionImpactPage = saved.ImpactPage;
        junctionReviewedAction = saved.ReviewedAction;
        junctionPlan = null;
        if (saved.Candidate.Length > 0 && saved.Editor is "magic" or "gf" or "command" or "passive")
        {
            PreviewJunctionCandidate(saved.Candidate);
        }
        else if (saved.Review && saved.Editor == "optimize")
        {
            PlanJunctionOptimization(saved.Priority);
        }
        else if (saved.Review && saved.Editor == "fill")
        {
            PlanJunctionCommands();
        }
        else if (saved.Review && saved.Editor == "actions" && saved.ReviewedAction is { } action)
        {
            ReviewJunctionAction(action);
        }
        junctionReview = saved.Review && junctionPlan is not null;
        junctionTransferModal = saved.TransferModal && junctionPlan is not null;
        junctionFocus = saved.Focus;
        junctionDashboard.SetDirection(JunctionEffectFamily.Elemental, saved.ElementalDirection);
        junctionDashboard.SetDirection(JunctionEffectFamily.Status, saved.StatusDirection);
        _view = null;
    }

    private bool TryExecuteJunctionDashboard(MenuAction action)
    {
        if (!IsOpen || !IsJunctionDashboard || action.Kind != "junction-ui")
        {
            return false;
        }
        SelectJunctionControl(action.SubjectId, false);
        UiButton? button = FocusableDescendants(BuildView().Root).Where(JunctionControlIsActive).OfType<UiButton>().FirstOrDefault(element => element.Id == action.SubjectId);
        if (button?.Enabled == true)
        {
            button.Click?.Invoke();
        }
        return true;
    }

    private void SelectJunctionControl(string id, bool allowDisabled)
    {
        UiView view = BuildView();
        UiElement? control = FocusableDescendants(view.Root).FirstOrDefault(element => element.Id == id);
        if (control is null || !JunctionControlIsActive(control) || !allowDisabled && !control.Enabled)
        {
            throw new InvalidOperationException($"Junction dashboard has no enabled control '{id}'.");
        }
        if (junctionEditor is "magic" or "command" or "passive")
        {
            junctionCandidateColumns?.EnsureVisible(control);
        }
        junctionFocus = id;
        view.Focus(control);
        if (id.StartsWith("gf-portrait-", StringComparison.Ordinal))
        {
            PreviewGuardian(id[12..]);
        }
    }

    private UiElement BuildJunctionAttackModes()
    {
        UiGrid choices = Grid([1, 1, 1], [1], 5);
        Place(choices, JunctionLabel(J("attack-mode", "Basic action · Attack mode"), 18), 0);
        Place(choices, JunctionButton("physical-mode", MagicText("PhysicalOnly"), () => SetJunctionAttackMode("PhysicalOnly")), 1);
        Place(choices, JunctionButton("smart-mode", MagicText("SmartMagic") + " · " + J("smart-dependency", "Requires usable Magic or Draw; otherwise falls back to physical."), () => SetJunctionAttackMode("SmartMagic"), size: 14), 2);
        return choices;
    }

    private void SetJunctionAttackMode(string mode)
    {
        if (Character is not { } actor)
        {
            return;
        }
        if (actor.AttackMode == mode)
        {
            CancelJunctionEditor();
            return;
        }
        string previousMode = actor.AttackMode;
        Execute(new MenuAction("attack-mode", TargetId: actor.Id, Value: mode));
        if (Character?.AttackMode != mode || Data.JunctionPlanner is not { } planner)
        {
            return;
        }
        junctionAttackUndo = (actor.Id, previousMode, planner.SourceFingerprint);
        junctionUndoLabel = J("attack-mode", "Attack mode");
        junctionVisited.Add(actor.Id);
        junctionUndo = null;
        CancelJunctionEditor();
    }
}
