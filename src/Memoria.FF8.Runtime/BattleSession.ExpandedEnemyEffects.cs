namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool CanPresentExpandedEnemyEffect(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction action, int recipientCount)
    {
        return recipientCount == 1 && (IsExpandedOriginalGraphicPresentation(actor, command, action) || IsExpandedAuthoredDeathEffectPresentation(actor, command, action)) ||
            command is { Ability: BattleAbility.EnemyAbility, NativeSequence: 16 } && actor is { IsPlayer: false, ModelId: 44 } &&
            action is { Command: 8, KernelSection: 3, ActionId: 175, HitCount: 1 } && recipientCount == 1 ||
            command.Ability == BattleAbility.EnemyAbility && command.NativeSequence == 11 && actor is { IsPlayer: false, ModelId: 99 } &&
            action is { Command: 8, ActionId: 182, MagicId: NativeBattleSonic139Profile.Effect, HitCount: 1 } &&
            recipientCount is >= 1 and <= NativeBattleSonic139Profile.HostedTargetCapacity;
    }

    private bool IsExpandedOriginalGraphicPresentation(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction action)
    {
        // NG-089: The authored reserve uses its physical scene rows and supplies a zero contact for cinematic context.
        return UsesOriginalEnemySlots && definition.EncounterId == 164 && actor is { IsPlayer: false, ModelId: 71, SceneSlot: 3 } &&
            command is { Ability: BattleAbility.EnemyAbility, NativeGraphic: not null, NativeAiCallbackStage: 0 } &&
            action is { Command: 252, ActionId: 1, MagicId: 248, HitCount: 1 } &&
            command.TargetRequest.Selection is BattleTargetSelection.Explicit { Actors: { Length: 1 } recipients } && recipients[0].ActorId == actor.Id;
    }

    private bool IsExpandedAuthoredDeathEffectPresentation(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction action)
    {
        // NG-090: The authored death action schedules activation of the loaded fourth enemy.
        return UsesOriginalEnemySlots && command is { Ability: BattleAbility.EnemyAbility, NativeAiCallbackStage: 3, NativeSequence: 12 } &&
            action is { Command: 8, KernelSection: 3, ActionId: 287, MagicId: 20, HitCount: 1 } &&
            command.TargetRequest.DeathCallbackSelf?.ActorId == actor.Id && FindActiveNativeActor(6) is { IsPlayer: false, Hidden: true, Suspended: true, Untargetable: true };
    }
}
