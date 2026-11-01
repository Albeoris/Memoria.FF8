using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool junctionTransferModal;
    private bool junctionRestoringFocus;
    private bool JunctionHasActiveScope => junctionTransferModal || junctionExitReview || junctionReview || junctionEditor is "magic" or "command" or "passive" or "actions" or "optimize" or "fill" or "attack-mode";
    private bool JunctionHasTransferModal => junctionTransferModal;

    private void PreviewGuardian(string id)
    {
        if (JunctionHasActiveScope || Character is null || Data.GuardianForces.All(force => force.Id != id))
        {
            return;
        }
        if (junctionEditor == "gf" && junctionCandidate == id && junctionPlan is { } current && Data.JunctionPlanner?.IsCurrent(current) == true)
        {
            return;
        }
        junctionEditor = "gf";
        junctionOrigin = "gf-portrait-" + id;
        PreviewJunctionCandidate(id);
    }

    private void LeaveGuardianPreview(string nextId)
    {
        if (junctionRestoringFocus || JunctionHasActiveScope || junctionEditor != "gf")
        {
            return;
        }
        bool assignment = nextId.StartsWith("slot-", StringComparison.Ordinal) || nextId.StartsWith("command-", StringComparison.Ordinal) || nextId.StartsWith("passive-", StringComparison.Ordinal) || nextId == "basic-action";
        if (!assignment && nextId is not ("previous-character" or "next-character"))
        {
            return;
        }
        junctionPlan = null;
        junctionEditor = "";
        junctionCandidate = "";
        junctionOrigin = nextId;
        Message = "";
        _view = null;
    }

    private void ActivateGuardian(string id)
    {
        if (JunctionHasActiveScope)
        {
            return;
        }
        PreviewGuardian(id);
        if (junctionPlan is not { HasChanges: true })
        {
            return;
        }
        MenuGuardianForce? force = Data.GuardianForces.FirstOrDefault(value => value.Id == id);
        if (force?.Owner is { } owner && owner.Id != Character?.Id)
        {
            junctionTransferModal = true;
            junctionReview = true;
            junctionImpactPage = 0;
            junctionFocus = "junction-transfer-no";
            _view = null;
            return;
        }
        ConfirmJunctionPlan();
    }

    private void ActivateJunctionCandidate(string id)
    {
        if (junctionEditor is not ("magic" or "command" or "passive"))
        {
            return;
        }
        if (junctionCandidate != id || junctionPlan is null || Data.JunctionPlanner?.IsCurrent(junctionPlan) != true)
        {
            PreviewJunctionCandidate(id);
        }
        if (junctionPlan is { HasChanges: true })
        {
            ConfirmJunctionPlan();
        }
        else if (junctionPlan?.UnavailableReason is null)
        {
            CancelJunctionEditor();
        }
    }

    private UiElement BuildJunctionBindingHints()
    {
        UiGrid hints = Grid([1, 1, 1, 1], [1], 3);
        string confirm = junctionTransferModal ? J("confirm-transfer", "Confirm transfer") : junctionReview ? J("apply", "Apply changes") : junctionEditor is "magic" or "command" or "passive" ? J("assign", "Assign") : J("edit", "Edit");
        Place(hints, JunctionLabel(CurrentBindingText(ControlAction.Confirm) + "  " + confirm, 13), 0);
        Place(hints, JunctionLabel(CurrentBindingText(ControlAction.Cancel) + "  " + (JunctionHasActiveScope ? J("back", "Back") : J("close", "Close")), 13), 1);
        Place(hints, JunctionLabel(CurrentBindingText(ControlAction.Card) + "  " + J("actions", "Actions"), 13, JunctionHasActiveScope), 2);
        Place(hints, JunctionLabel(CurrentBindingText(ControlAction.CameraLeft) + " / " + CurrentBindingText(ControlAction.CameraRight) + "  " + J("character", "Character"), 12, JunctionHasActiveScope), 3);
        return hints;
    }

    private bool JunctionControlIsActive(UiElement element)
    {
        string id = element.Id ?? "";
        if (junctionTransferModal)
        {
            return id is "junction-transfer-yes" or "junction-transfer-no" or "junction-review" or "impact-previous" or "impact-next";
        }
        if (junctionExitReview)
        {
            return id == "leave-configured" || id.StartsWith("configure-", StringComparison.Ordinal);
        }
        if (junctionReview)
        {
            return id is "junction-review" or "impact-previous" or "impact-next";
        }
        if (junctionEditor is "magic" or "command" or "passive")
        {
            return id.StartsWith("candidate-", StringComparison.Ordinal);
        }
        if (junctionEditor == "actions")
        {
            return id is "optimize" or "fill-commands" or "pin-stat" or "junction-undo" or "clear-magic" or "remove-junctions" or "clear-party-magic";
        }
        if (junctionEditor == "optimize")
        {
            return id.StartsWith("priority-", StringComparison.Ordinal);
        }
        if (junctionEditor == "attack-mode")
        {
            return id is "physical-mode" or "smart-mode";
        }
        return true;
    }

    private void ApplyJunctionInteractionScope(UiView view)
    {
        foreach (UiElement control in FocusableDescendants(view.Root))
        {
            if (!JunctionControlIsActive(control))
            {
                control.Enabled = false;
            }
        }
    }

    private UiElement? BuildJunctionTransferModal()
    {
        if (!junctionTransferModal || junctionPlan is not { } plan)
        {
            return null;
        }
        UiGrid body = JunctionRows([64, 0, 38], [1], 10);
        MenuGuardianForce? force = Data.GuardianForces.FirstOrDefault(value => value.Id == junctionCandidate);
        string question = string.Format(System.Globalization.CultureInfo.CurrentCulture, J("transfer-question", "Remove {0} from {1} and junction to {2}?"), force?.Name ?? "", force?.Owner?.Name ?? "", Character?.Name ?? "");
        UiLabel title = JunctionLabel(question, 20);
        title.TextFlow = UiTextFlow.Wrap;
        Place(body, title, 0);
        Place(body, BuildJunctionImpactList(), 1);
        UiGrid choices = Grid([1], [1, 1], 14);
        Place(choices, JunctionButton("junction-transfer-yes", J("yes", "Yes"), () =>
        {
            junctionTransferModal = false;
            ConfirmJunctionPlan();
        }, plan.HasChanges), 0);
        Place(choices, JunctionButton("junction-transfer-no", J("no", "No"), CancelJunctionEditor), 0, 1);
        Place(body, choices, 2);
        UiPanel panel = new() { CaptionHeight = 0, Padding = 18 };
        panel.Children.Add(body);
        return new JunctionTransferOverlay(panel, TextScale) { Id = "junction-transfer-modal" };
    }

    private sealed class JunctionTransferOverlay : UiElement
    {
        private readonly float scale;

        public JunctionTransferOverlay(UiElement content, float scale)
        {
            this.scale = scale;
            Children.Add(content);
        }

        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            float width = Math.Min(bounds.Width - 32, 760 * scale);
            float height = Math.Min(bounds.Height - 32, 400 * scale);
            Children[0].Arrange(new UiRect(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height));
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Fill(Bounds, new UiColor(0, 0, 0, 180));
        }
    }
}
