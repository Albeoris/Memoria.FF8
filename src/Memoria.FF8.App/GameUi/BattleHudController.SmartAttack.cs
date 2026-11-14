using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private BattleSmartAttackPreview? displayedSmartPreview;

    public string PhysicalOverrideBindingHint { get; set; } = new KeyboardInput().BindingPrompt(ControlAction.CameraLeft);
    public bool ForcePhysical => !HasGuardianFormCommands && SelectingTarget && SelectedCommand == BattleAbility.Attack && (nativeCommandHeldButtons & 0x03) == 0x01;
    public BattleSmartAttackPreview? TargetActionPreview
    {
        get
        {
            if (HasGuardianFormCommands || !SelectingTarget || SelectedCommand != BattleAbility.Attack || SelectedActor is not { } actor || SelectedTarget is not { } target)
            {
                return null;
            }
            displayedSmartPreview = session.PreviewSmartAttack(actor.Id, target.Id, ForcePhysical);
            return displayedSmartPreview;
        }
    }
    private BattleSmartAttackPreview? ConfirmedAttackPreview()
    {
        return displayedSmartPreview is { } preview && preview.ActorId == SelectedActor?.Id && preview.TargetId == SelectedTarget?.Id && preview.ForcePhysical == ForcePhysical ? preview : TargetActionPreview;
    }

    private string SmartAttackDisplayName(BattleSmartAttackPreview preview)
    {
        if (preview.Ability == BattleAbility.Attack)
        {
            BattleCombatant actor = session.Combatants.Single(actor => actor.Id == preview.ActorId);
            BattleCommandPresentation command = BattleCommandPresentation.Read(actor).First(choice => choice.Ability == BattleAbility.Attack);
            return FormatBattleText(localizedCommandName?.Invoke(command.Command, preview.Name) ?? preview.Name);
        }
        BattleActionDefinition? action = session.ActionDefinitions.FirstOrDefault(action => action.Ability == BattleAbility.Magic && action.Id == preview.ActionId);
        string name = action is null ? preview.Name : localizedActionName?.Invoke(action) ?? action.DisplayName ?? action.Name;
        if (preview.Ability == BattleAbility.Draw && action is not null)
        {
            BattleCommandPresentation draw = SelectedActor is { } actor ? BattleCommandPresentation.Read(actor).FirstOrDefault(choice => choice.Ability == BattleAbility.Draw) ?? BattleCommandPresentation.Default(BattleAbility.Draw) : BattleCommandPresentation.Default(BattleAbility.Draw);
            name = (localizedCommandName?.Invoke(draw.Command, draw.Name) ?? draw.Name) + ": " + name;
        }
        return FormatBattleText(name);
    }

    public string TargetActionCaption
    {
        get
        {
            if (HasGuardianFormCommands)
            {
                return GuardianFormCommandLabel;
            }
            BattleSmartAttackPreview? preview = TargetActionPreview;
            if (preview is not null)
            {
                return SmartAttackDisplayName(preview) + (preview.CastCount > 1 ? $" ×{preview.CastCount}" : "");
            }
            return (SelectedAction is { } action ? ChoiceLabel(action) : CommandLabel(SelectedCommand)) + (SelectedCommand == BattleAbility.Magic && magicCastCount > 1 ? $" ×{magicCastCount}" : "");
        }
    }
    public string PhysicalOverrideCaption => HasGuardianFormCommands ? "" : SelectedCommand == BattleAbility.Attack ? FormatSystemText("magic.PhysicalOverride", MagicUiText.Get("PhysicalOverride"), PhysicalOverrideBindingHint) : "";
}
