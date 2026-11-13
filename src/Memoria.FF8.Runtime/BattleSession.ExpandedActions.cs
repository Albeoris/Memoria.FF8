using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record ExpandedBattleContact(string RecipientId, NativeBattleContactHalf Contact);

public sealed partial class BattleSession
{
    private Func<BattleCombatant, BattleNativeAction, ImmutableArray<ExpandedBattleContact>, NativeBattleEffectPresentation>? expandedEffectFactory;
    private NativeBattleEffectPresentation? expandedEffect;
    private NativeBattleEffectContactBridge<string>? expandedContactBridge;
    private readonly HashSet<string> expandedPresentedRecipients = new(StringComparer.Ordinal);
    public NativeBattleEffectPresentation? ExpandedEffect => expandedEffect;

    public void AttachExpandedEffectFactory(Func<BattleCombatant, BattleNativeAction, ImmutableArray<ExpandedBattleContact>, NativeBattleEffectPresentation> factory)
    {
        if (!UsesExpandedRoster || expandedEffectFactory is not null)
        {
            throw new InvalidOperationException("The expanded battle presentation owner is unavailable or already attached.");
        }
        expandedEffectFactory = factory;
    }

    public bool SupportsExpandedAction(BattleActionDefinition action)
    {
        return action.NativeAction is { HitCount: 1 } native &&
            (action.Ability, native.Command, native.ActionId, native.MagicId) is
                (BattleAbility.Magic, 2, 1, _) or (BattleAbility.Magic, 2, _, 38) or (BattleAbility.Item, 4, 1, _);
    }

    private void RequireExpandedAction(BattleActionDefinition action)
    {
        if (!SupportsExpandedAction(action))
        {
            throw new NotSupportedException("The expanded battle host requires a single native contact per selected target and a supported formula.");
        }
        if (action.Ability == BattleAbility.Magic && expandedEffectFactory is null)
        {
            throw new InvalidOperationException("The expanded battle has no attached native effect presentation.");
        }
    }

    private void StartExpandedAction(BattleCombatant actor, BattleAttackCommand command)
    {
        if (!CanNativeBattleActorAct(actor) || !CanExecuteMagicReservation(actor, command) || !ActionHasValidTarget(command.TargetId, command.Ability, command.ActionId, command.NativeAction, 0, aiRecipients: command.AiRecipients))
        {
            RefundCancelledNativeItem(command);
            actor.IsBusy = false;
            return;
        }
        ActiveAction = NativeCommandPresentation(command) with
        {
            RemainingFrames = NativeActionWindupFrames(actor, command.NativeSequence),
            NativeSequenceFrame = NativeControlTrack(actor, command.NativeSequence) is null ? null : 0
        };
        string name = command.NativeAction?.Name ?? ActionName(command.Ability, command.ActionId);
        Log("Windup", actor.Id, command.TargetId, 0, $"{actor.Name}: {name}.");
    }

    private void AdvanceExpandedAction(BattleActionPresentation command)
    {
        if (expandedEffect is { } effect)
        {
            NativeBattleEffectPresentationStep step = effect.AdvanceCurrent();
            foreach (NativeBattleModuleSound sound in step.Sounds)
            {
                PublishNativeModuleSound(sound, command.ActorId);
            }
            foreach (NativeBattleContactPacket packet in step.Contacts)
            {
                string recipient = expandedContactBridge!.Resolve(packet);
                if (!expandedPresentedRecipients.Add(recipient))
                {
                    throw new InvalidDataException("The expanded effect repeated a published recipient.");
                }
                string actionName = ActionName(command.Ability, command.ActionId);
                Log("EffectContact", command.ActorId, recipient, packet.Primary.PresentedAmount, actionName);
                if (packet.Primary.PresentedAmount > 0)
                {
                    bool healing = (packet.Primary.HitFlags3 & 1) != 0;
                    Log(healing ? "EffectHealing" : "EffectDamage", command.ActorId, recipient, packet.Primary.PresentedAmount, actionName);
                }
            }
            if (effect.Complete)
            {
                if (expandedPresentedRecipients.Count != expandedContactBridge!.Count)
                {
                    throw new InvalidDataException("The expanded effect completed before publishing all prepared contacts.");
                }
                expandedEffect = null;
                expandedContactBridge = null;
                expandedPresentedRecipients.Clear();
                CompleteActionPresentation(command, "Expanded effect completed.");
            }
            return;
        }
        BattleCombatant actor = GetActor(command.ActorId);
        if (command.NativeSequenceFrame is int frame && NativeControlTrack(actor, command.NativeSequence) is { } track && frame < track.CompletionFrame)
        {
            command = AdvanceNativeControlSequence(command);
            ActiveAction = command;
            if (command.NativeSequenceFrame < track.CompletionFrame)
            {
                return;
            }
        }
        else if (command.RemainingFrames > 1)
        {
            ActiveAction = command with { RemainingFrames = command.RemainingFrames - 1 };
            return;
        }
        BattleCombatant target = GetActor(command.TargetId);
        if (!CanNativeBattleActorAct(actor) || !CanExecuteMagicReservation(actor, new(command.ActorId, command.TargetId, command.Ability, command.ActionId) { MagicReservationId = command.MagicReservationId }) || (command.AiRecipients is { } aiTargets ? CurrentExpandedAiTargets(aiTargets).Length == 0 : !target.IsAlive || target.Untargetable) ||
            (command.Ability == BattleAbility.Magic || command.NativeAction?.Command == 2) && (NativeSavedStatusBits(actor) & 0x10) != 0)
        {
            RefundCancelledNativeItem(command);
            CompleteActionPresentation(command, "Expanded action cancelled.");
            return;
        }
        if (command.Ability == BattleAbility.Attack && actor.NativeAttack is null)
        {
            ResolveAction(actor, target, command);
            CompleteActionPresentation(command, "Attack completed.");
            return;
        }

        BattleActionDefinition action = command.Ability == BattleAbility.Attack
            ? new(BattleAbility.Attack, 0, actor.NativeAttack!.Name ?? "Attack", BattleActionEffect.Native, actor.NativeAttack.Power, false) { NativeAction = actor.NativeAttack }
            : command.AiRecipients is not null
                ? new(BattleAbility.EnemyAbility, command.ActionId, command.NativeAction!.Name ?? $"Enemy ability {command.ActionId}", BattleActionEffect.Native, command.NativeAction.Power, false) { NativeAction = command.NativeAction }
                : FindAction(command.Ability, command.ActionId);
        BattleNativeAction native = action.NativeAction!;
        BattleCombatant[] recipients = command.AiRecipients is { } selected ? CurrentExpandedAiTargets(selected) : action.AllTargets
            ? Roster(target.Side).Where(candidate => candidate.IsActive && candidate.IsAlive && !candidate.Untargetable).ToArray()
            : [target];
        if (recipients.Any(recipient => NativeActionReflects(recipient, native)))
        {
            RefundCancelledNativeItem(command);
            CompleteActionPresentation(command, "The expanded host cannot present this reflected command.");
            return;
        }

        // The effect bank is action-local. It does not own any battle roster slot or action memory.
        // AI contacts are consumed immediately by the host and do not enter the presentation bank.
        int nextRow = recipients.Length == 1 ? NativeBattleSlots.AllyCount : 1;
        ImmutableArray<ExpandedBattleContact>.Builder contacts = ImmutableArray.CreateBuilder<ExpandedBattleContact>(recipients.Length);
        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false, EventLane = actor.IsPlayer ? (byte)2 : (byte)0, Command = command.NativeQueuedCommand ?? native.Command, Action = native.ActionId };
        BattleActionPresentation resolved = command with { NativeAction = native };
        resolvingNativeAction = resolved;
        nativeActionCommitDepth++;
        try
        {
            foreach (BattleCombatant recipient in recipients)
            {
                byte row = command.AiRecipients is not null || recipient == actor ? (byte)0 : checked((byte)nextRow++);
                recipient.NativeRemainingHits = 1;
                NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, recipient, resolved, action, native, 255, presentationRow: row);
                contacts.Add(new(recipient.Id, packet.Primary));
            }
            CompleteNativeActionInventory(actor, resolved, false);
            CompleteNativeActionStatuses(actor);
        }
        finally
        {
            nativeActionCommitDepth--;
            resolvingNativeAction = null;
        }
        if (command.Ability == BattleAbility.Attack)
        {
            CompleteActionPresentation(command, "Attack completed.");
            return;
        }
        if (command.Ability is BattleAbility.Item or BattleAbility.EnemyAbility)
        {
            // TODO(TD-009): Expanded enemy abilities and Potion use the host impact UI pending native presentation admission.
            foreach (ExpandedBattleContact contact in contacts)
            {
                Log("EffectContact", command.ActorId, contact.RecipientId, contact.Contact.PresentedAmount, action.Name);
            }
            CompleteActionPresentation(command, "Action completed.");
            return;
        }
        ImmutableArray<ExpandedBattleContact> prepared = contacts.MoveToImmutable();
        expandedContactBridge = new(prepared.Select(contact => (contact.RecipientId, contact.Contact)));
        expandedEffect = expandedEffectFactory!(actor, native, prepared);
        ActiveAction = command with { Phase = BattleActionPhase.Recovery, RemainingFrames = 1, MagicReservationId = null };
    }
}
