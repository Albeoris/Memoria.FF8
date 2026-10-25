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
                return preview.Name + (preview.CastCount > 1 ? $" ×{preview.CastCount}" : "");
            }
            return (SelectedAction?.Name ?? CommandLabel(SelectedCommand)) + (SelectedCommand == BattleAbility.Magic && magicCastCount > 1 ? $" ×{magicCastCount}" : "");
        }
    }
    public string PhysicalOverrideCaption => HasGuardianFormCommands ? "" : SelectedCommand == BattleAbility.Attack ? MagicUiText.Format("PhysicalOverride", PhysicalOverrideBindingHint) : "";
}
