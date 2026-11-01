using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed record JunctionDashboardNavigationState(string Editor, string Origin, string Candidate, bool Review, string Priority, string Focus, int CandidatePage, int ImpactPage, string Query, string Sort, JunctionEffectDirection ElementalDirection, JunctionEffectDirection StatusDirection);

public sealed partial class MenuController
{
    private IReadOnlyList<MenuEntry> JunctionDashboardEntries() => FocusableDescendants(BuildView().Root).OfType<UiButton>().Select(button => new MenuEntry(button.Id ?? "", button.Text, J("select-preview", "Select · Preview"), Action: new MenuAction("junction-ui", button.Id ?? ""), Enabled: button.Enabled)).ToArray();

    internal JunctionDashboardNavigationState CaptureJunctionDashboard() => new(junctionEditor, junctionOrigin, junctionCandidate, junctionReview, junctionPriority, junctionFocus, junctionCandidatePage, junctionImpactPage, junctionSearch, junctionSort, junctionDashboard.ElementalDirection, junctionDashboard.StatusDirection);

    internal void RestoreJunctionDashboard(JunctionDashboardNavigationState saved)
    {
        if (!IsJunctionDashboard)
        {
            return;
        }
        junctionEditor = saved.Editor;
        junctionOrigin = saved.Origin;
        junctionPriority = saved.Priority;
        junctionSearch = saved.Query;
        junctionSort = saved.Sort;
        junctionCandidatePage = saved.CandidatePage;
        junctionImpactPage = saved.ImpactPage;
        junctionPlan = null;
        if (saved.Origin.StartsWith("slot-", StringComparison.Ordinal))
        {
            _junctionSlot = saved.Origin[5..];
        }
        if (saved.Candidate.Length > 0 && saved.Editor is "magic" or "gf" or "command" or "passive")
        {
            PreviewJunctionCandidate(saved.Candidate);
        }
        else if (saved.Review && saved.Editor == "optimize")
        {
            PlanJunctionOptimization(saved.Priority);
        }
        junctionReview = saved.Review;
        junctionFocus = saved.Focus;
        junctionDashboard.SetDirection(JunctionEffectFamily.Elemental, saved.ElementalDirection);
        junctionDashboard.SetDirection(JunctionEffectFamily.Status, saved.StatusDirection);
        _view = null;
    }

    private bool TryExecuteJunctionDashboard(MenuAction action)
    {
        if (!IsJunctionDashboard || action.Kind != "junction-ui")
        {
            return false;
        }
        SelectJunctionControl(action.SubjectId, false);
        UiButton? button = FocusableDescendants(BuildView().Root).OfType<UiButton>().FirstOrDefault(element => element.Id == action.SubjectId);
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
        if (control is null && id.StartsWith("candidate-", StringComparison.Ordinal))
        {
            int candidate = JunctionCandidates().ToList().FindIndex(value => "candidate-" + value.Id == id);
            if (candidate >= 0)
            {
                junctionCandidatePage = candidate / JunctionCandidatePageSize;
                junctionFocus = id;
                _view = null;
                view = BuildView();
                control = FocusableDescendants(view.Root).FirstOrDefault(element => element.Id == id);
            }
        }
        if (control is null || !allowDisabled && !control.Enabled)
        {
            throw new InvalidOperationException($"Junction dashboard has no enabled control '{id}'.");
        }
        junctionFocus = id;
        view.Focus(control);
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
        Execute(new MenuAction("attack-mode", TargetId: actor.Id, Value: mode));
        junctionVisited.Add(actor.Id);
        junctionAudit.MarkAffected(actor.Id);
        junctionUndo = null;
        CancelJunctionEditor();
    }
}
