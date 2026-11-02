using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool junctionTransferModal;
    private UiColumnPicker? junctionTransferColumns;
    private bool junctionRestoringFocus;
    private bool JunctionHasActiveScope => junctionTransferModal || junctionReview || junctionEditor is "magic" or "command" or "passive" or "actions" or "optimize" or "fill" or "attack-mode";
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
        if (junctionReview && junctionEditor == "optimize")
        {
            UiGrid hints = new() { Gap = 16 * TextScale };
            hints.RowDefinitions.Add(UiGridLength.Star());
            hints.ColumnDefinitions.Add(UiGridLength.Auto);
            hints.ColumnDefinitions.Add(UiGridLength.Auto);
            hints.ColumnDefinitions.Add(UiGridLength.Star());
            UiButton apply = JunctionButton("junction-apply", CurrentBindingText(ControlAction.Confirm) + "  " + J("apply-short", "Apply"), ConfirmJunctionPlan, size: 13);
            UiButton cancel = JunctionButton("junction-cancel", CurrentBindingText(ControlAction.Cancel) + "  " + J("cancel", "Cancel"), CancelJunctionEditor, size: 13);
            apply.DrawBackground = false;
            cancel.DrawBackground = false;
            Place(hints, apply, 0);
            Place(hints, cancel, 0, 1);
            return hints;
        }
        UiLabel hint = JunctionLabel(CurrentBindingText(ControlAction.Card) + "  " + J("actions", "Actions"), 13, JunctionHasActiveScope);
        hint.HorizontalAlignment = UiAlignment.End;
        hint.VerticalAlignment = UiAlignment.End;
        return hint;
    }

    private bool JunctionControlIsActive(UiElement element)
    {
        string id = element.Id ?? "";
        if (junctionTransferModal)
        {
            return id is "junction-transfer-yes" or "junction-transfer-no" || id.StartsWith("transfer-loss-", StringComparison.Ordinal);
        }
        if (junctionReview)
        {
            if (junctionEditor == "optimize")
            {
                return id is "junction-review" or "junction-apply" or "junction-cancel";
            }
            return id is "junction-review" or "impact-previous" or "impact-next";
        }
        if (junctionEditor is "magic" or "command" or "passive")
        {
            return id.StartsWith("candidate-", StringComparison.Ordinal);
        }
        if (junctionEditor == "actions")
        {
            return id is "optimize" or "clear-magic" or "clear-party-magic";
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
        UiGrid body = JunctionRows([84 * TextScale, 0, 38 * TextScale], [1], 10);
        MenuGuardianForce? force = Data.GuardianForces.FirstOrDefault(value => value.Id == junctionCandidate);
        UiGrid transfer = new() { Gap = 16 * TextScale };
        transfer.RowDefinitions.Add(UiGridLength.Star());
        transfer.ColumnDefinitions.Add(UiGridLength.Pixel(84 * TextScale));
        transfer.ColumnDefinitions.Add(UiGridLength.Star());
        transfer.ColumnDefinitions.Add(UiGridLength.Pixel(84 * TextScale));
        transfer.ColumnDefinitions.Add(UiGridLength.Star());
        transfer.ColumnDefinitions.Add(UiGridLength.Pixel(84 * TextScale));
        UiImage donorPortrait = new()
        {
            Id = "junction-transfer-donor",
            Source = force?.Owner?.NativeId is int ownerId ? new UiImageSource(UiImageKind.CharacterPortrait, ownerId) : null
        };
        Place(transfer, donorPortrait, 0);
        Place(transfer, new JunctionTransferArrow(), 0, 1);
        Place(transfer, new UiImage
        {
            Id = "junction-transfer-guardian",
            Source = force?.NativeId is int nativeId ? new UiImageSource(UiImageKind.GuardianForcePortrait, nativeId) : null
        }, 0, 2);
        Place(transfer, new JunctionTransferArrow(), 0, 3);
        UiImage recipientPortrait = Portrait(Character);
        recipientPortrait.Id = "junction-transfer-recipient";
        Place(transfer, recipientPortrait, 0, 4);
        Place(body, transfer, 0);
        MenuCharacter? donor = plan.Before.Characters.FirstOrDefault(actor => actor.Id == force?.Owner?.Id);
        junctionTransferColumns = null;
        Place(body, BuildJunctionGuardianCapabilities(donor ?? throw new InvalidOperationException("Guardian transfer requires its current owner.")), 1);
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

    private sealed class JunctionTransferArrow : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            float y = Bounds.Y + Bounds.Height / 2;
            float tip = Math.Min(12, Bounds.Width / 3);
            UiColor color = new(225, 230, 235);
            canvas.Line(Bounds.X, y, Bounds.Right, y, color, 2);
            canvas.Line(Bounds.Right - tip, y - tip / 2, Bounds.Right, y, color, 2);
            canvas.Line(Bounds.Right - tip, y + tip / 2, Bounds.Right, y, color, 2);
        }
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
            float width = Math.Min(bounds.Width - 32, 840 * scale);
            float height = Math.Min(bounds.Height - 32, 440 * scale);
            Children[0].Arrange(new UiRect(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height));
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Fill(Bounds, new UiColor(0, 0, 0, 180));
        }
    }
}
