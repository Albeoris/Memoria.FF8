using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private MenuAction? pendingGuardianForceTransfer;

    internal MenuGuardianForceOwner? GuardianForceOwner(string guardianForceId)
    {
        if (Data.GuardianForces.FirstOrDefault(force => force.Id == guardianForceId)?.Owner is { } projected)
        {
            return projected;
        }
        MenuCharacter? owner = Data.Characters.FirstOrDefault(character => character.GuardianForceIds.Contains(guardianForceId));
        return owner is null ? null : new MenuGuardianForceOwner(owner.Id, owner.NativeId, owner.Name);
    }

    private bool ExecuteGuardianForceTransfer(MenuAction action)
    {
        if (action.Kind == "junction-gf-confirm")
        {
            MenuAction? transfer = pendingGuardianForceTransfer;
            if (ScreenId != "junction-gf-confirm" || transfer is null)
            {
                return true;
            }
            _liveData = null;
            bool valid = Character?.Id == transfer.TargetId && GuardianForceOwner(transfer.SubjectId)?.Id == transfer.Value && Data.GuardianForces.Any(force => force.Id == transfer.SubjectId);
            Back();
            if (!valid)
            {
                SetSystemMessage(() => SystemText("ui.executeguardianforcetransfer.message.gf.ownership.changed.select.the.guardian.force.again", "GF ownership changed. Select the Guardian Force again."));
                return true;
            }
            Execute(transfer);
            return true;
        }
        if (action.Kind != "junction-gf" || Character is not { } destination || GuardianForceOwner(action.SubjectId) is not { } owner || owner.Id == destination.Id)
        {
            return false;
        }
        MenuGuardianForce? force = Data.GuardianForces.FirstOrDefault(force => force.Id == action.SubjectId);
        if (force is null)
        {
            return true;
        }
        pendingGuardianForceTransfer = new MenuAction("junction-gf-reassign", force.Id, destination.Id, owner.Id);
        string question = FormatSystemText("ui.executeguardianforcetransfer.question.remove.from.and.junction.it.to", "Remove {0} from {1} and junction it to {2}?", force.Name, owner.Name, destination.Name);
        Register(new MenuScreenDefinition
        {
            Id = "junction-gf-confirm",
            Title = "Transfer GF",
            Help = question,
            HelpText = c => c.FormatSystemText("junction.transfer.question", "Remove {0} from {1} and junction it to {2}?", force.Name, owner.Name, destination.Name),
            Entries = _ =>
            [
                new MenuEntry("no", SystemText("ui.executeguardianforcetransfer.entry.label.no", "No"), SystemText("ui.executeguardianforcetransfer.entry.help.keep.the.current.junction", "Keep the current junction."), Action: new MenuAction("menu-back")),
                new MenuEntry("yes", SystemText("ui.executeguardianforcetransfer.entry.label.yes", "Yes"), question, Action: new MenuAction("junction-gf-confirm"))
            ],
            BuildContent = (menus, root) =>
            {
                var anchor = new UiAnchorPanel();
                var content = Grid([3, 2], [1], 12);
                UiLabel prompt = menus.Label(question, 24);
                prompt.TextFlow = UiTextFlow.Wrap;
                Place(content, prompt, 0);
                Place(content, menus.Commands(36, 24), 1);
                UiPanel panel = menus.Panel(SystemText("ui.executeguardianforcetransfer.panel.transfer.gf", "Transfer GF"), content);
                panel.Anchors = new UiAnchors(0.1f, 0.12f, 0.9f, 0.88f);
                anchor.Children.Add(panel);
                root.Children.Add(anchor);
            }
        });
        Push("junction-gf-confirm");
        Select(0, Entries.Count);
        return true;
    }
}
